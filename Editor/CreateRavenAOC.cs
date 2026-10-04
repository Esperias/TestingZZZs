using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class CreateRavenAOC
{
    [MenuItem("Tools/Create Raven AOC")]
    public static void Create()
    {
        string path = "Assets/CreaturesCharactersBundle/Raven/RavenController.controller";
        UnityEditor.Animations.AnimatorController controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(path);

        controller.AddParameter("IsFlying", UnityEngine.AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsGliding", UnityEngine.AnimatorControllerParameterType.Bool);
        controller.AddParameter("Speed", UnityEngine.AnimatorControllerParameterType.Float);
        
        AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;
        
        AnimationClip GetClip(string p) {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(p);
            foreach (Object asset in assets) {
                if (asset is AnimationClip && !asset.name.StartsWith("__preview__")) return asset as AnimationClip;
            }
            return null;
        }

        // Load ALL clips
        AnimationClip idle1 = GetClip("Assets/CreaturesCharactersBundle/Raven/Animations/AS_idle_01.fbx");
        AnimationClip idle2 = GetClip("Assets/CreaturesCharactersBundle/Raven/Animations/AS_idle_02.fbx");
        AnimationClip peck = GetClip("Assets/CreaturesCharactersBundle/Raven/Animations/AS_pecking.fbx");
        AnimationClip fly = GetClip("Assets/CreaturesCharactersBundle/Raven/Animations/AS_gliding.fbx"); // Turns out this pack's gliding anim is actually flapping
        AnimationClip glide = GetClip("Assets/CreaturesCharactersBundle/Raven/Animations/AS_flying.fbx"); // And flying is gliding!
        AnimationClip takeoff = GetClip("Assets/CreaturesCharactersBundle/Raven/Animations/AS_flying_start.fbx");
        AnimationClip land = GetClip("Assets/CreaturesCharactersBundle/Raven/Animations/AS_flying_landing.fbx");
        AnimationClip walk = GetClip("Assets/CreaturesCharactersBundle/Raven/Animations/AS_walk.fbx");
        AnimationClip walkJump = GetClip("Assets/CreaturesCharactersBundle/Raven/Animations/AS_walk_jump.fbx");
        AnimationClip flyTransStart = GetClip("Assets/CreaturesCharactersBundle/Raven/Animations/AS_flying_trans_start.fbx");
        AnimationClip flyTransEnd = GetClip("Assets/CreaturesCharactersBundle/Raven/Animations/AS_flying_trans_end.fbx");
        
        // Create states
        AnimatorState stateIdle = rootStateMachine.AddState("Idle");
        stateIdle.motion = idle1;
        
        AnimatorState stateIdle2 = rootStateMachine.AddState("Idle2");
        stateIdle2.motion = idle2;

        AnimatorState statePeck = rootStateMachine.AddState("Pecking");
        statePeck.motion = peck;
        
        AnimatorState stateTakeoff = rootStateMachine.AddState("TakeOff");
        stateTakeoff.motion = takeoff;
        
        AnimatorState stateFlyTransStart = rootStateMachine.AddState("FlyTransStart");
        stateFlyTransStart.motion = flyTransStart;

        AnimatorState stateFly = rootStateMachine.AddState("Fly");
        stateFly.motion = fly;
        
        AnimatorState stateFlyTransEnd = rootStateMachine.AddState("FlyTransEnd");
        stateFlyTransEnd.motion = flyTransEnd;
        
        AnimatorState stateGlide = rootStateMachine.AddState("Glide");
        stateGlide.motion = glide;
        
        AnimatorState stateLand = rootStateMachine.AddState("Land");
        stateLand.motion = land;

        AnimatorState stateWalk = rootStateMachine.AddState("Walk");
        stateWalk.motion = walk;
        
        AnimatorState stateWalkJump = rootStateMachine.AddState("WalkJump");
        stateWalkJump.motion = walkJump;

        // --- Helper to setup transition ---
        AnimatorStateTransition AddTrans(AnimatorState from, AnimatorState to, bool hasExitTime, float exitTime, float duration)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = hasExitTime;
            t.exitTime = exitTime;
            t.hasFixedDuration = true;
            t.duration = duration;
            return t;
        }

        // --- Transitions ---
        
        // Idle -> Takeoff (Instant, triggered by IsFlying)
        var transToTakeoff = AddTrans(stateIdle, stateTakeoff, false, 0f, 0.1f);
        transToTakeoff.AddCondition(AnimatorConditionMode.If, 0, "IsFlying");
        
        // Takeoff -> FlyTransStart (Plays until the end)
        AddTrans(stateTakeoff, stateFlyTransStart, true, 0.85f, 0.15f);

        // FlyTransStart -> Fly
        AddTrans(stateFlyTransStart, stateFly, true, 0.85f, 0.15f);

        // Fly <-> Glide
        var flyToGlide = AddTrans(stateFly, stateGlide, false, 0f, 0.35f);
        flyToGlide.AddCondition(AnimatorConditionMode.If, 0, "IsGliding");

        var glideToFly = AddTrans(stateGlide, stateFly, false, 0f, 0.35f);
        glideToFly.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGliding");

        // Fly -> Land (Triggered by !IsFlying)
        var transToTransEndFly = AddTrans(stateFly, stateFlyTransEnd, false, 0f, 0.15f);
        transToTransEndFly.AddCondition(AnimatorConditionMode.IfNot, 0, "IsFlying");

        // Glide -> Land (Triggered by !IsFlying)
        var transToTransEndGlide = AddTrans(stateGlide, stateFlyTransEnd, false, 0f, 0.15f);
        transToTransEndGlide.AddCondition(AnimatorConditionMode.IfNot, 0, "IsFlying");

        // FlyTransEnd -> Land
        AddTrans(stateFlyTransEnd, stateLand, true, 0.85f, 0.15f);

        // Land -> Idle
        AddTrans(stateLand, stateIdle, true, 0.85f, 0.15f);
        
        // Idle2 -> Idle (auto return)
        AddTrans(stateIdle2, stateIdle, true, 0.85f, 0.25f);

        // Pecking -> Idle (auto return)
        AddTrans(statePeck, stateIdle, true, 0.85f, 0.25f);

        // Walk transitions
        var transToWalk = AddTrans(stateIdle, stateWalk, false, 0f, 0.2f);
        transToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        
        var transWalkToIdle = AddTrans(stateWalk, stateIdle, false, 0f, 0.2f);
        transWalkToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        
        // Idle -> WalkJump (random variation triggered by script)
        AddTrans(stateWalkJump, stateIdle, true, 0.85f, 0.25f);

        Debug.Log("Raven Animator Controller updated with transition clips and proper exit times!");
    }
}
