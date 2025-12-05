using System;
using Nyteshade.Modules.Anim.Godot_Specific;
using System.Collections.Generic;
using NyteshadeGodot.Modules.Maths;
using System.Numerics;

using Nyteshade.Modules.Anim;
using UnityEngine;

public partial class Anim_GodotProgram : MonoBehaviour
{
    //Core Components
    private Skeleton _skeleton;
    private Nyteshade.Modules.Anim.AnimationPlayer _player;
    private GameObject _rigRoot;
    private MeshSkinner _skinner;
    //private Skeleton3D _godotSkeleton;
    private bool _isPaused = false;
    private bool _spaceHeld = false; // debounce flag

    //Animation Graph Variables
    private Dictionary<string, Nyteshade.Modules.Anim.AnimationClip> _animLibrary = new Dictionary<string, Nyteshade.Modules.Anim.AnimationClip>();
    
    // Locomotion nodes
    private LerpNode _locomotionRoot;
    private LerpNode _idleWalkBlend;
    private LerpNode _walkRunBlend;
    private LayeredAddNode _turningLayer;
    private LerpNode _turningBlend; 
    
    private ClipNode _jumpNode;
    private ClipNode _danceNode;
    private bool _triggerJump = false;
    private bool _isDancing = false; // Back to a toggle
    
    // Input state variables
    private float _currentSpeed = 0.0f;
    private float _turnAmount = 0.0f; 
    
    //IK Fields
    private IKManager _ikManager;
    private LookAtIKSolver _lookSolver;
    private CCDIK_Solver _leftArmSolver; 
    private System.Numerics.Vector3 _lookTargetPosGui = new System.Numerics.Vector3(0, -1.5f, 0f);
    private float _lookAtWeight = 1.0f; 
    private System.Numerics.Vector3 _grabTargetPosGui = new System.Numerics.Vector3(0.5f, 1.0f, -0.5f);
    private float _grabWeight = 0.0f; 
    private int _chainSelection = 0;


  public void Start()
    {
        Debug.Log("=== Nyteshade Animation Runtime Initialized ===");

        // ... (Steps 1-5 are identical to your file) ...
        // 1. Load rig
        SkinnedMeshRenderer rigScene = FindFirstObjectByType<SkinnedMeshRenderer>();

        // 2. Build skeleton
        _skeleton = GodotBridge.BuildSkeleton(rigScene.rootBone);

        // 3. Find Godot Skeleton
        _godotSkeleton =
            _rigRoot.GetNodeOrNull<Skeleton3D>("Armature/Skeleton3D") ??
            _rigRoot.GetNodeOrNull<Skeleton3D>("Skeleton3D") ??
            _rigRoot.GetNodeOrNull<Skeleton3D>("RootNode/Skeleton3D");
        if (_godotSkeleton == null) GD.PrintErr("[Bridge] No Skeleton3D found in rig hierarchy!");

        // 4. Load Animation Library
        Debug.Log("[Init] Loading animation library...");
        _animLibrary["Idle"] = LoadClip("res://Assets/Animations/Idle.glb", "mixamo_com");
        _animLibrary["Walk"] = LoadClip("res://Assets/Animations/Walk.glb", "mixamo_com");
        _animLibrary["Slow_Run"] = LoadClip("res://Assets/Animations/Slow_Run.glb", "mixamo_com");
        _animLibrary["Turn_Left"] = LoadClip("res://Assets/Animations/Turn_Left.glb", "mixamo_com");
        _animLibrary["Turn_Right"] = LoadClip("res://Assets/Animations/Turn_Right.glb", "mixamo_com");
        _animLibrary["Jump"] = LoadClip("res://Assets/Animations/Jump.glb", "mixamo_com");
        _animLibrary["Dance"] = LoadClip("res://Assets/Animations/Dance.glb", "mixamo_com");

        Debug.Log($"[Init] Loaded {_animLibrary.Count} animation clips.");

        // 5. Initialize player + skinner
        _player = new Nyteshade.Modules.Anim.AnimationPlayer(_skeleton);
        _skinner = new MeshSkinner(_skeleton);

        // ─────────────────────────────────────────────
        // 6. Build Full Animation Graph
        // ─────────────────────────────────────────────
        GD.Print("[Init] Building animation graph...");

        // --- 6a. Create Base Pose Node ---
        var node_Base = new BasePoseNode(_skeleton);

        // --- 6b. Build the "Locomotion" state graph ---
        var node_Idle = new ClipNode(_animLibrary["Idle"]);
        var node_Walk = new ClipNode(_animLibrary["Walk"]);
        var node_Run = new ClipNode(_animLibrary["Slow_Run"]);
        var node_TurnLeft = new ClipNode(_animLibrary["Turn_Left"]);
        var node_TurnRight = new ClipNode(_animLibrary["Turn_Right"]);

        _idleWalkBlend = new LerpNode(node_Idle, node_Walk);
        _walkRunBlend = new LerpNode(node_Walk, node_Run);
        _locomotionRoot = new LerpNode(_idleWalkBlend, _walkRunBlend);
        _turningBlend = new LerpNode(node_TurnLeft, node_TurnRight);
        _turningLayer = new LayeredAddNode(_locomotionRoot, _turningBlend, _skeleton.BoneCount);

        int[] torsoChain = BuildChain("mixamorig_Head", "mixamorig_Spine");
        if (torsoChain != null)
        {
            foreach (int boneIndex in torsoChain)
            {
                _turningLayer.BoneWeights[boneIndex] = 1.0f;
            }
        }
        // --- 6c. Build the "Jump" state graph ---
        _jumpNode = new ClipNode(_animLibrary["Jump"]);
        _jumpNode.Loops = false;

        // --- 6d. Build the "Dance" state graph ---
        _danceNode = new ClipNode(_animLibrary["Dance"]);

        // [MODIFIED] Set Dance to be a one-shot, not a loop
        _danceNode.Loops = false;

        // --- 6e. Create States ---
        var state_Locomotion = new AnimState(_turningLayer);
        var state_Jump = new AnimState(_jumpNode);
        var state_Dance = new AnimState(_danceNode);

        // --- 6f. Define Transitions ---

        // From Locomotion:
        state_Locomotion.AddTransition("Jump", () =>
        {
            bool didTrigger = _triggerJump;
            _triggerJump = false; // Consume the trigger
            return didTrigger;
        }, 0.1f); // Fast blend *into* the jump

        state_Locomotion.AddTransition("Dance", () =>
        {
            bool didTrigger = _isDancing;
            _isDancing = false; // Consume the trigger
            return didTrigger;
        }, 0.3f); // 0.3s blend *into* the dance

        // From Jump:
        state_Jump.AddTransition("Locomotion", () => !_jumpNode.IsPlaying, 0.25f);

        // From Dance:
        // 1. (Interrupt) Pressing 'R' to walk/run
        state_Dance.AddTransition("Locomotion", () => Input.IsKeyPressed(Key.R), 0.5f); // 0.5s "oh, gotta go" blend
        // 2. (Auto-Exit) The dance animation finishes
        state_Dance.AddTransition("Locomotion", () => !_danceNode.IsPlaying, 0.5f);

        // --- 6g. Create the State Machine ---
        var stateMachine = new StateMachineNode("Locomotion", state_Locomotion);
        stateMachine.AddState("Jump", state_Jump);
        stateMachine.AddState("Dance", state_Dance);

        // --- 6h. Set the Final Graph Root ---
        var rootNode = new AddNode(node_Base, stateMachine);
        _player.SetRoot(rootNode);

        // ─────────────────────────────────────────────
        //  7. Init IK
        // ─────────────────────────────────────────────
        GD.Print("[Init] Setting up IK...");
        _ikManager = new IKManager(_skeleton);

        int neckIndex = _skeleton.GetBoneIndex("mixamorig_Neck");
        if (neckIndex != -1)
        {
            var localForward = new System.Numerics.Vector3(0, 0, 1);
            _lookSolver = new LookAtIKSolver(neckIndex, localForward);
            _ikManager.AddSolver(_lookSolver);
            GD.Print($"[IK] Added LookAt solver for bone 'mixamorig_Neck' (Index: {neckIndex})");
        }
        else
        {
            GD.PrintErr($"[IK] Failed to find bone 'mixamorig_Neck' for LookAt solver.");
        }
        RebuildLeftArmSolver(_chainSelection);
    }

    private AnimationClip LoadClip(string filePath, string animNameInFile)
    {
        var scene = GD.Load<PackedScene>(filePath);
        if (scene == null)
        {
            GD.PrintErr($"[AnimLoader] Failed to load animation scene: {filePath}");
            return null;
        }
        var root = scene.Instantiate<Node3D>();
        var clip = GodotBridge.BuildClipFromAnimationPlayer(root, _skeleton, animNameInFile, 30f);
        root.QueueFree();
        if (clip == null) GD.PrintErr($"[AnimLoader] Failed to build clip '{animNameInFile}' from {filePath}");
        return clip;
    }

    public override void _Process(double delta)
    {
        // ... (Pause logic is the same) ...
        if (Input.IsKeyPressed(Key.Space))
        {
            if (!_spaceHeld)
            {
                _spaceHeld = true;
                _isPaused = !_isPaused;
                GD.Print(_isPaused ? "[Runtime] Animation paused." : "[Runtime] Animation resumed.");
            }
        }
        else _spaceHeld = false;

        if (!_isPaused)
        {
            // 1. Input Pass
            HandleInput((float)delta);
            // 2. Update Graph
            UpdateAnimationGraph((float)delta);

            // 3. Animation Pass (Ticks the state machine)
            _player.Update((float)delta);

            // 4. IK Pass
            if (_ikManager != null)
            {
                UpdateIKSolvers();
                _ikManager.ResolveSolvers();
            }

            // 5. Skinning Pass
            _skinner.UpdateSkinning();

            // 6. Apply to Godot
            GodotBridge.ApplyBoneOverridesFromSkeleton(_godotSkeleton, _skeleton);
        }

        // Debug Draw (always active)
        DebugDrawBoneMotion();
    }

    private void HandleInput(float delta)
    {
        // --- Locomotion ---
        bool isWalking = Input.IsKeyPressed(Key.R);
        bool isRunning = Input.IsKeyPressed(Key.Shift);
        float targetSpeed = 0.0f;
        if (isWalking)
        {
            targetSpeed = isRunning ? 1.0f : 0.5f;
        }
        _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, delta * 5.0f);

        // --- Turning ---
        bool isTurningLeft = Input.IsKeyPressed(Key.Left);
        bool isTurningRight = Input.IsKeyPressed(Key.Right);

        float targetTurn = 0.0f;
        if (isTurningLeft) targetTurn = -1.0f;
        else if (isTurningRight) targetTurn = 1.0f;
        _turnAmount = Mathf.Lerp(_turnAmount, targetTurn, delta * 7.0f);

        // --- State Triggers ---

        // Jump (V key) - This is a "one-shot" trigger
        if (Input.IsKeyLabelPressed(Key.V))
        {
            if (!_jumpNode.IsPlaying && !_danceNode.IsPlaying)
            {
                _triggerJump = true;
                GD.Print("[Input] Jump Triggered!");
            }
        }

        if (Input.IsKeyLabelPressed(Key.B))
        {
            if (!_jumpNode.IsPlaying && !_danceNode.IsPlaying)
            {
                _isDancing = true;
                GD.Print($"[Input] Dance Triggered!");
            }
        }
    }

    // ... UpdateAnimationGraph is unchanged ...
    private void UpdateAnimationGraph(float delta)
    {
        // --- Update Locomotion ---
        float idleWalkBlend = Mathf.Clamp(_currentSpeed * 2.0f, 0.0f, 1.0f);
        float walkRunBlend = Mathf.Clamp((_currentSpeed - 0.5f) * 2.0f, 0.0f, 1.0f);
        _idleWalkBlend.BlendWeight = idleWalkBlend;
        _walkRunBlend.BlendWeight = walkRunBlend;
        _locomotionRoot.BlendWeight = walkRunBlend;

        // --- Update Turning ---
        float turnBlend = (_turnAmount + 1.0f) * 0.5f;
        _turningBlend.BlendWeight = turnBlend;
        _turningLayer.Weight = Mathf.Abs(_turnAmount);
    }

    private void UpdateIKSolvers()
    {
        // --- Update Look Solver ---
        if (_lookSolver != null)
        {
            var targetPosMeters = _lookTargetPosGui;
            targetPosMeters.Z = -1.0f; // Lock Z
            var targetPosCentimeters = new System.Numerics.Vector3(
                targetPosMeters.X * 100.0f, targetPosMeters.Z * -100.0f, targetPosMeters.Y * 100.0f);
            _lookSolver.SetTarget(targetPosCentimeters);
            _lookSolver.Weight = _lookAtWeight;
        }
        // --- Update Grab Solver ---
        if (_leftArmSolver != null)
        {
            var targetPosMeters = _grabTargetPosGui;
            var targetPosCentimeters = new System.Numerics.Vector3(
                targetPosMeters.X * 100.0f, targetPosMeters.Z * -100.0f, targetPosMeters.Y * 100.0f);
            _leftArmSolver.SetTarget(targetPosCentimeters);
            _leftArmSolver.Weight = _grabWeight;
        }
    }

    private int[] BuildChain(string endBoneName, string rootBoneName)
    {
        int endIndex = _skeleton.GetBoneIndex(endBoneName);
        int rootIndex = _skeleton.GetBoneIndex(rootBoneName);
        if (endIndex == -1 || rootIndex == -1)
        {
            GD.PrintErr($"[IK] Failed to build chain. Could not find '{endBoneName}' or '{rootBoneName}'.");
            return null;
        }
        var indices = new List<int>();
        Transform current = _skeleton.GetBone(endIndex);
        for (int i = 0; i < _skeleton.BoneCount; i++)
        {
            if (current == null)
            {
                GD.PrintErr($"[IK] Failed to build chain. Bone '{endBoneName}' is not a child of '{rootBoneName}'.");
                return null;
            }
            indices.Add(_skeleton.GetBoneIndex(current.Name));
            if (current.Name == rootBoneName)
            {
                indices.Reverse();
                return indices.ToArray();
            }
            current = current.Parent;
        }
        GD.PrintErr($"[IK] Failed to build chain. Hit iteration limit.");
        return null;
    }

    // ... RebuildLeftArmSolver ...
    private void RebuildLeftArmSolver(int selection)
    {
        if (_leftArmSolver != null)
        {
            _ikManager.RemoveSolver(_leftArmSolver);
            _leftArmSolver = null;
        }
        int[] chainIndices = null;
        string chainDesc = "";
        switch (selection)
        {
            case 0: // Grab with Hand
                chainIndices = BuildChain("mixamorig_LeftHand", "mixamorig_LeftArm");
                chainDesc = "Hand (Full Arm)";
                break;
            case 1: // Grab with Elbow
                chainIndices = BuildChain("mixamorig_LeftForeArm", "mixamorig_LeftArm");
                chainDesc = "Elbow (Forearm)";
                break;
            case 2: // Grab with Shoulder
                chainIndices = BuildChain("mixamorig_LeftArm", "mixamorig_LeftShoulder");
                chainDesc = "Shoulder (Upper Arm)";
                break;
            case 3: // Wrist
                chainIndices = BuildChain("mixamorig_LeftHand", "mixamorig_LeftForeArm");
                chainDesc = "Wrist (Hand Only)";
                break;
        }

        if (chainIndices != null && chainIndices.Length > 0)
        {
            _leftArmSolver = new CCDIK_Solver(chainIndices);
            _leftArmSolver.Weight = _grabWeight;
            _ikManager.AddSolver(_leftArmSolver);
            GD.Print($"[IK] Built CCD solver for Left Arm (chain: {chainDesc})");
        }
        else
        {
            GD.PrintErr($"[IK] Failed to build chain for selection {selection}.");
        }*/
    }
}