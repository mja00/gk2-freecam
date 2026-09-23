using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using Cinemachine;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace GK2Freecam;

[BepInPlugin(Guid, "GK2 Freecam", "1.0.0")]
public class FreecamPlugin : BaseUnityPlugin
{
	public const string Guid = "matta.gk2.freecam";

	private static readonly AccessTools.FieldRef<LazyInput> InputInstance = AccessTools.StaticFieldRefAccess<LazyInput>(AccessTools.Field(typeof(LazyInput), "instance"));
	private static readonly AccessTools.FieldRef<LazyInput, List<GameKey>> HeldKeys = AccessTools.FieldRefAccess<LazyInput, List<GameKey>>("holdedKeys");
	private static readonly AccessTools.FieldRef<LazyInput, Vector2> Direction = AccessTools.FieldRefAccess<LazyInput, Vector2>("direction");
	private static readonly AccessTools.FieldRef<LazyInput, Vector2> Direction2 = AccessTools.FieldRefAccess<LazyInput, Vector2>("direction2");

	private ConfigEntry<KeyboardShortcut> toggleKey;
	private ConfigEntry<KeyboardShortcut> projectionKey;
	private ConfigEntry<KeyboardShortcut> resetKey;
	private ConfigEntry<KeyboardShortcut> hudKey;
	private ConfigEntry<float> moveSpeed;
	private ConfigEntry<float> fastMultiplier;
	private ConfigEntry<float> lookSensitivity;
	private ConfigEntry<float> perspectiveFov;
	private ConfigEntry<bool> showHint;

	private bool active;
	private bool hideHint;
	private Camera cam;
	private CinemachineBrain brain;
	private float yaw;
	private float pitch;

	private Vector3 savedPosition;
	private Quaternion savedRotation;
	private bool savedOrthographic;
	private float savedOrthoSize;
	private float savedFov;
	private float savedNear;
	private float savedFar;
	private bool savedInputActive;

	private void Awake()
	{
		toggleKey = Config.Bind("Keys", "Toggle", new KeyboardShortcut(KeyCode.F7), "Enter/exit freecam.");
		projectionKey = Config.Bind("Keys", "ToggleProjection", new KeyboardShortcut(KeyCode.P), "Switch orthographic/perspective while in freecam.");
		resetKey = Config.Bind("Keys", "Reset", new KeyboardShortcut(KeyCode.R), "Snap back to the game's camera pose while in freecam.");
		hudKey = Config.Bind("Keys", "ToggleHint", new KeyboardShortcut(KeyCode.H), "Show/hide the on-screen controls hint while in freecam.");
		moveSpeed = Config.Bind("Movement", "Speed", 8f, "Units per second.");
		fastMultiplier = Config.Bind("Movement", "FastMultiplier", 4f, "Speed multiplier while Shift is held.");
		lookSensitivity = Config.Bind("Movement", "LookSensitivity", 2f, "Degrees per mouse unit while right mouse is held.");
		perspectiveFov = Config.Bind("Camera", "PerspectiveFov", 50f, "Starting field of view when switching to perspective.");
		showHint = Config.Bind("UI", "ShowHint", true, "Draw a controls hint while freecam is active.");
		Logger.LogInfo($"Loaded. Press {toggleKey.Value} in game to toggle freecam.");
	}

	private void Update()
	{
		if (toggleKey.Value.IsDown())
		{
			if (active)
			{
				Deactivate();
			}
			else
			{
				TryActivate();
			}
		}

		if (!active)
		{
			return;
		}

		// The world camera is rebuilt on scene loads; bail out instead of driving a destroyed object.
		if (cam == null || brain == null)
		{
			active = false;
			LazyInput.SetInputActivity(savedInputActive);
			return;
		}

		if (resetKey.Value.IsDown())
		{
			ApplySavedPose();
		}
		if (projectionKey.Value.IsDown())
		{
			SetPerspective(cam.orthographic);
		}
		if (hudKey.Value.IsDown())
		{
			hideHint = !hideHint;
		}

		UpdateLook();
		UpdateMove();
		UpdateZoom();
	}

	private void LateUpdate()
	{
		// Chunk streaming, fighting agents and UI bubbles all listen for brain updates; keep them in sync while the brain is off.
		if (active && brain != null)
		{
			CinemachineCore.CameraUpdatedEvent.Invoke(brain);
		}
	}

	private void TryActivate()
	{
		CameraSystem system = CameraSystem.Instance;
		if (system == null || system.MainCamera == null || system.WorldCamera == null)
		{
			Logger.LogWarning("No world camera yet; load into the game first.");
			return;
		}

		cam = system.WorldCamera;
		brain = cam.GetComponent<CinemachineBrain>();
		if (brain == null)
		{
			Logger.LogWarning("World camera has no CinemachineBrain; refusing to take over.");
			return;
		}

		Transform t = cam.transform;
		savedPosition = t.position;
		savedRotation = t.rotation;
		savedOrthographic = cam.orthographic;
		savedOrthoSize = cam.orthographicSize;
		savedFov = cam.fieldOfView;
		savedNear = cam.nearClipPlane;
		savedFar = cam.farClipPlane;
		savedInputActive = LazyInput.IsInputActive();

		brain.enabled = false;
		LazyInput.SetInputActivity(false);
		ClearGameInput();

		Vector3 euler = t.eulerAngles;
		yaw = euler.y;
		pitch = NormalizeAngle(euler.x);
		active = true;
		Logger.LogInfo("Freecam on.");
	}

	private void Deactivate()
	{
		active = false;
		Cursor.lockState = CursorLockMode.None;
		if (cam != null)
		{
			ApplySavedPose();
		}
		if (brain != null)
		{
			brain.enabled = true;
		}
		LazyInput.SetInputActivity(savedInputActive);
		Logger.LogInfo("Freecam off.");
	}

	private void ApplySavedPose()
	{
		cam.transform.SetPositionAndRotation(savedPosition, savedRotation);
		cam.orthographic = savedOrthographic;
		cam.orthographicSize = savedOrthoSize;
		cam.fieldOfView = savedFov;
		cam.nearClipPlane = savedNear;
		cam.farClipPlane = savedFar;
		Vector3 euler = savedRotation.eulerAngles;
		yaw = euler.y;
		pitch = NormalizeAngle(euler.x);
	}

	private void SetPerspective(bool perspective)
	{
		cam.orthographic = !perspective;
		if (perspective)
		{
			cam.fieldOfView = perspectiveFov.Value;
			cam.nearClipPlane = 0.1f;
			cam.farClipPlane = Mathf.Max(savedFar, 500f);
		}
		else
		{
			cam.nearClipPlane = savedNear;
			cam.farClipPlane = savedFar;
		}
	}

	// LazyInput stops updating when inactive, so anything held at toggle time would otherwise stay "held" (player keeps walking).
	private static void ClearGameInput()
	{
		LazyInput input = InputInstance();
		if (input == null)
		{
			return;
		}
		LazyInput.ClearAllKeysDown();
		HeldKeys(input).Clear();
		Direction(input) = Vector2.zero;
		Direction2(input) = Vector2.zero;
	}

	private void UpdateLook()
	{
		if (Input.GetMouseButtonDown(1))
		{
			Cursor.lockState = CursorLockMode.Locked;
		}
		if (Input.GetMouseButtonUp(1))
		{
			Cursor.lockState = CursorLockMode.None;
		}
		if (!Input.GetMouseButton(1))
		{
			return;
		}
		yaw += Input.GetAxisRaw("Mouse X") * lookSensitivity.Value;
		pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * lookSensitivity.Value, -89f, 89f);
		cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
	}

	// WASD pans on the ground plane relative to yaw, since flying along view direction is invisible in orthographic mode.
	private void UpdateMove()
	{
		Quaternion heading = Quaternion.Euler(0f, yaw, 0f);
		Vector3 move = Vector3.zero;
		if (Input.GetKey(KeyCode.W)) move += heading * Vector3.forward;
		if (Input.GetKey(KeyCode.S)) move += heading * Vector3.back;
		if (Input.GetKey(KeyCode.D)) move += heading * Vector3.right;
		if (Input.GetKey(KeyCode.A)) move += heading * Vector3.left;
		if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space)) move += Vector3.up;
		if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftControl)) move += Vector3.down;
		if (move == Vector3.zero)
		{
			return;
		}

		float speed = moveSpeed.Value;
		if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
		{
			speed *= fastMultiplier.Value;
		}
		// Unscaled so the camera still flies while the game is paused or in slow motion.
		cam.transform.position += move.normalized * speed * Time.unscaledDeltaTime;
	}

	private void UpdateZoom()
	{
		float scroll = Input.mouseScrollDelta.y;
		if (scroll == 0f)
		{
			return;
		}
		float factor = Mathf.Pow(0.9f, scroll);
		if (cam.orthographic)
		{
			cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * factor, 0.5f, 200f);
		}
		else
		{
			cam.fieldOfView = Mathf.Clamp(cam.fieldOfView * factor, 5f, 120f);
		}
	}

	private void OnGUI()
	{
		if (!active || hideHint || !showHint.Value)
		{
			return;
		}
		GUI.Label(new Rect(10f, 10f, 700f, 22f), $"FREECAM  WASD move | E/Q up/down | Shift fast | RMB look | Scroll zoom | {projectionKey.Value} proj | {resetKey.Value} reset | {hudKey.Value} hide | {toggleKey.Value} exit");
	}

	private static float NormalizeAngle(float angle)
	{
		if (angle > 180f)
		{
			return angle - 360f;
		}
		return angle;
	}
}
