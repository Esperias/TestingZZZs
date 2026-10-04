using Random = UnityEngine.Random;
using Object = UnityEngine.Object;
using UnityEngine;

public class NPCWanderer : MonoBehaviour
{
	private enum State
	{
		Idle,
		Walking
	}

	private Animator animator;

	private Collider ground;

	private int speedHash;

	private float edgeMargin;

	private float walkSpeed;

	private float turnSpeed;

	private float wanderRadius;

	private float minIdleTime;

	private float maxIdleTime;

	private State state;

	private Vector3 target;

	private float idleTimer;

	private float walkTimer;

	private Transform faceTarget;

	private Renderer[] renderers;

	private Vector3 localCenter;

	private float zoneRadius;

	private float zoneHalfHeight;

	private bool hidden;

	private const float ArriveDistance = 0.3f;

	private const float MaxWalkTime = 20f;

	public bool IsHidden => hidden;

	public Vector3 homePosition;
	public void Init(Collider groundCollider, float margin, float speed, float turn, float radius, float idleMin, float idleMax, string speedParam)
	{
		homePosition = transform.position;
		ground = groundCollider;
		edgeMargin = margin;
		walkSpeed = speed;
		turnSpeed = turn;
		wanderRadius = radius;
		minIdleTime = idleMin;
		maxIdleTime = idleMax;
		speedHash = Animator.StringToHash(speedParam);
		CacheHideZone();
		animator = ((Component)this).GetComponentInChildren<Animator>();
		if ((UnityEngine.Object)(object)animator != (UnityEngine.Object)null)
		{
			animator.applyRootMotion = false;
			animator.cullingMode = (AnimatorCullingMode)2;
		}
		SnapToGround();
		StartIdle();
	}

	private void Update()
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		if ((UnityEngine.Object)(object)faceTarget != (UnityEngine.Object)null)
		{
			Vector3 val = faceTarget.position - ((Component)this).transform.position;
			val.y = 0f;
			if (val.sqrMagnitude > 0.001f)
			{
				((Component)this).transform.rotation = Quaternion.Slerp(((Component)this).transform.rotation, Quaternion.LookRotation(val), turnSpeed * 2f * Time.deltaTime);
			}
			SetAnimSpeed(0f);
			return;
		}
		if (state == State.Idle)
		{
			idleTimer -= Time.deltaTime;
			SetAnimSpeed(0f);
			if (idleTimer <= 0f)
			{
				StartWalking();
			}
		}
		else
		{
			WalkToTarget();
		}
		SnapToGround();
	}

	private void CacheHideZone()
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		renderers = ((Component)this).GetComponentsInChildren<Renderer>();
		if (renderers.Length != 0)
		{
			Bounds bounds = renderers[0].bounds;
			for (int i = 1; i < renderers.Length; i++)
			{
				bounds.Encapsulate(renderers[i].bounds);
			}
			localCenter = ((Component)this).transform.InverseTransformPoint(bounds.center);
			zoneRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
			zoneHalfHeight = bounds.extents.y;
		}
	}

	public void UpdateVisibility(Vector3 cameraPos, float padding, bool forceVisible)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		if (renderers == null || renderers.Length == 0)
		{
			return;
		}
		bool flag = false;
		if (!forceVisible)
		{
			Vector3 val = cameraPos - ((Component)this).transform.TransformPoint(localCenter);
			Vector2 val2 = new Vector2(val.x, val.z);
			flag = val2.magnitude <= zoneRadius + padding && Mathf.Abs(val.y) <= zoneHalfHeight + padding;
		}
		if (flag == hidden)
		{
			return;
		}
		hidden = flag;
		Renderer[] array = renderers;
		foreach (Renderer val3 in array)
		{
			if ((UnityEngine.Object)(object)val3 != (UnityEngine.Object)null)
			{
				val3.enabled = !hidden;
			}
		}
	}

	public void FaceAndStop(Transform target)
	{
		faceTarget = target;
	}

	public void Resume()
	{
		faceTarget = null;
	}

	private void StartIdle()
	{
		state = State.Idle;
		idleTimer = Random.Range(minIdleTime, maxIdleTime);
	}

	private void StartWalking()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		Vector3 position = homePosition;
		Vector2 val = Random.insideUnitCircle * wanderRadius;
		target = new Vector3(position.x + val.x, ((Component)this).transform.position.y, position.z + val.y);
		walkTimer = 20f;
		state = State.Walking;
	}

	private void WalkToTarget()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = target - ((Component)this).transform.position;
		val.y = 0f;
		walkTimer -= Time.deltaTime;
		if (val.magnitude <= 0.3f || walkTimer <= 0f)
		{
			StartIdle();
			SetAnimSpeed(0f);
			return;
		}
		Quaternion val2 = Quaternion.LookRotation(val);
		((Component)this).transform.rotation = Quaternion.Slerp(((Component)this).transform.rotation, val2, turnSpeed * Time.deltaTime);
		Transform transform = ((Component)this).transform;
		transform.position += ((Component)this).transform.forward * (walkSpeed * Time.deltaTime);
		SetAnimSpeed(walkSpeed);
	}

	private void SetAnimSpeed(float value)
	{
		if ((UnityEngine.Object)(object)animator != (UnityEngine.Object)null)
		{
			animator.SetFloat(speedHash, value);
		}
	}

	private void SnapToGround()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		Vector3 position = ((Component)this).transform.position;
		if ((UnityEngine.Object)(object)WorldGenerator.Instance != (UnityEngine.Object)null)
		{
			position.y = WorldGenerator.Instance.GetHeightAt(position.x, position.z);
			((Component)this).transform.position = position;
		}
	}
}





