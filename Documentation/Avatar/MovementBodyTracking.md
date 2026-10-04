# Movement SDK body recording

The Quest recording path now uses Meta XR Movement SDK full-body tracking. The values for elbows,
waist, pelvis and legs are inferred by Meta from the headset and controllers on Quest 2; they are
not additional measured sensor values.

## One-time editor setup

1. Run `Tools > Smart Factory > Avatar Demo > Configure Movement Body Tracking`.
2. Run `Tools > Smart Factory > Avatar Demo > Setup or Repair Avatar Demo` if the first command did
   not automatically refresh the scene.
3. In `Meta > Tools > Project Setup Tool`, inspect Android/Meta recommendations. Body tracking must be
   supported, the Body Tracking permission must be requested, tracking origin must be Floor Level,
   Body Tracking Fidelity must be High, and Body Tracking Joint Set must be Full Body.

The configuration command generates the Movement retargeting JSON for `Worker_Avatar.prefab`, attaches
`MovementBodyPoseStream` and `CharacterRetargeter`, and keeps `FullBodyAvatarIK` for legacy playback.

## File compatibility

- FVR v1: unchanged legacy frames; replay automatically enables `FullBodyAvatarIK`.
- FVR v2: keeps the original timestamp, tracking, load, task and pooled-part block in the same order,
  then appends the 84-joint Movement pose and source T-pose.
- v2 frames without a valid body pose temporarily fall back to `FullBodyAvatarIK`.
- Replay interpolates joint positions with `Vector3.Lerp` and rotations with `Quaternion.Slerp` before
  the Movement Character Retargeter applies the Humanoid pose.

Replay Session File (No Headset), Open Spectator Window, Pause, Restart and F8 continue to use the
existing replay and spectator code paths.
