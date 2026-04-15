using UnityEngine;
using System.Collections.Generic;
using EZhex1991.EZSoftBone;

public class ProceduralVillain : MonoBehaviour
{
	[Header("Player Reference")]
	public Transform player;

	[Header("Head Prefab")]
	[SerializeField] private GameObject headPrefab;
	[SerializeField] private Vector3 headPrefabScale = Vector3.one;
	[SerializeField] private Vector3 headPrefabRotationOffset = Vector3.zero;

	[Header("Villain Dimensions (Minecraft-style)")]
	[SerializeField] private float cubeSize = 0.2f;
	[SerializeField] private float characterHeight = 1.8f;

	[Header("Body Part Colors")]
	[SerializeField] private Color torsoColor = new Color(0.4f, 0.4f, 0.4f);
	[SerializeField] private Color armColor = new Color(0.8f, 0.7f, 0.6f);
	[SerializeField] private Color legColor = new Color(0.3f, 0.3f, 0.5f);

	[Header("Transformation Settings")]
	[SerializeField] private float activationDistance = 10f;
	[SerializeField] private float fullHorrorDistance = 3f;
	[SerializeField] private float transformSpeed = 2f;

	[Header("Cube Separation")]
	[SerializeField] private float maxCubeSeparation = 0.05f;

	[Header("Soft Bone Settings - Normal State")]
	[SerializeField] private float normalDamping = 0.8f;
	[SerializeField] private float normalStiffness = 0.95f;

	[Header("Soft Bone Settings - Horror State")]
	[SerializeField] private float horrorDamping = 0.15f;
	[SerializeField] private float horrorStiffness = 0.1f;
	[SerializeField] private Vector3 horrorGravity = new Vector3(0, -0.3f, 0);

	[Header("Horror Sound & Rat Sound")]
	[SerializeField] private AudioClip horrorSound;
	[SerializeField] private AudioClip ratSound;
	[SerializeField] private float ratSoundMinInterval = 0.5f;
	[SerializeField] private float ratSoundMaxInterval = 2f;

	[Header("Locomotion Blend")]
	[SerializeField] private float farStylizedDistance = 12f;
	[SerializeField] private float nearGroundedDistance = 5f;
	[SerializeField] private float runSpeedForFullBlend = 6f;

	[Header("Stylized Walk (Far)")]
	[SerializeField] private float walkCadence = 1.6f;
	[SerializeField] private float walkStepHeight = 0.2f;
	[SerializeField] private float walkLegSwing = 28f;
	[SerializeField] private float walkArmSwing = 16f;
	[SerializeField] private float spineRotationAmount = 5f;

	[Header("Grounded Run (Near)")]
	[SerializeField] private float runCadence = 3f;
	[SerializeField] private float runStride = 0.24f;
	[SerializeField] private float runLift = 0.08f;
	[SerializeField] private float footPlantStrength = 0.82f;
	[SerializeField] private float ikBlendSpeed = 12f;
	[SerializeField] private float floorRayStartHeight = 0.7f;
	[SerializeField] private float floorRayLength = 2.2f;
	[SerializeField] private LayerMask groundMask = ~0;

	[Header("Chase Dread Boost")]
	[SerializeField] private float chaseRunBlendBoost = 0.3f;
	[SerializeField] private float chaseCadenceMultiplier = 1.3f;
	[SerializeField] private float chaseStrideMultiplier = 1.2f;
	[SerializeField] private float chaseArmBalanceMultiplier = 1.35f;
	[SerializeField] private float chaseTorsoLeanMultiplier = 1.2f;

	[Header("Balance / Threat")]
	[SerializeField] private float torsoLeanNear = 14f;
	[SerializeField] private float torsoSwayAmount = 3f;
	[SerializeField] private float balanceArmSpread = 12f;
	[SerializeField] private float balanceArmSwing = 42f;
	[SerializeField] private float uncannyNearBend = 10f;
	[SerializeField] private float asymmetryAmount = 0.25f;

	[Header("Horror Head Effects")]
	[SerializeField] private float headWiggleSpeed = 15f;
	[SerializeField] private float headWiggleAmount = 25f;
	[SerializeField] private float headTwitchIntensity = 45f;
	[SerializeField] private float headTwitchSpeed = 12f;

	private Dictionary<string, List<GameObject>> bodyPartCubes = new Dictionary<string, List<GameObject>>();
	private Dictionary<string, EZSoftBone> bodyPartBones = new Dictionary<string, EZSoftBone>();
	private Dictionary<string, List<Vector3>> originalCubeOffsets = new Dictionary<string, List<Vector3>>();
	private GameObject headInstance;
	private AudioSource audioSource;
	private AudioSource ratAudioSource;

	private float currentHorrorLevel = 0f;
	private float walkCycle = 0f;
	private Vector3 lastPosition;
	private bool isMoving = false;
	private float nextRatSoundTime = 0f;
	private float worldMoveSpeed = 0f;
	private float nearBlend = 0f;
	private VillainAI villainAI;
	private bool chaseActive = false;

	private Transform skeleton;
	private Transform headBone;
	private Transform torsoBone;
	private Transform leftArmBone;
	private Transform rightArmBone;
	private Transform leftLegBone;
	private Transform rightLegBone;

	private float leftLegBaseY;
	private float rightLegBaseY;
	private float leftLegBaseX;
	private float rightLegBaseX;

	void Start()
	{
		audioSource = gameObject.AddComponent<AudioSource>();
		audioSource.clip = horrorSound;
		audioSource.loop = true;
		audioSource.spatialBlend = 1f;

		ratAudioSource = gameObject.AddComponent<AudioSource>();
		ratAudioSource.clip = ratSound;
		ratAudioSource.loop = false;
		ratAudioSource.spatialBlend = 1f;
		ratAudioSource.pitch = 1f;
		ratAudioSource.volume = 2f;

		lastPosition = transform.position;
		villainAI = GetComponent<VillainAI>();

		GenerateVillain();
		SetupEZSoftBones();
		CacheLegBaseOffsets();
	}

	void CacheLegBaseOffsets()
	{
		if (leftLegBone != null)
		{
			leftLegBaseY = leftLegBone.localPosition.y;
			leftLegBaseX = leftLegBone.localPosition.x;
		}
		if (rightLegBone != null)
		{
			rightLegBaseY = rightLegBone.localPosition.y;
			rightLegBaseX = rightLegBone.localPosition.x;
		}
	}

	void GenerateVillain()
	{
		skeleton = new GameObject("Skeleton").transform;
		skeleton.SetParent(transform);
		skeleton.localPosition = Vector3.zero;

		float headSize = cubeSize * 8f;
		float torsoWidth = cubeSize * 8f;
		float torsoHeight = cubeSize * 12f;
		float limbWidth = cubeSize * 4f;

		headBone = CreateBone("Head", skeleton, new Vector3(0, characterHeight - headSize / 2, 0));
		torsoBone = CreateBone("Torso", skeleton, new Vector3(0, characterHeight - headSize - torsoHeight / 2, 0));

		float armHeight = characterHeight - headSize - cubeSize * 2f;
		leftArmBone = CreateBone("LeftArm", skeleton, new Vector3(-torsoWidth / 2 - limbWidth / 2, armHeight, 0));
		rightArmBone = CreateBone("RightArm", skeleton, new Vector3(torsoWidth / 2 + limbWidth / 2, armHeight, 0));

		float legHeight = characterHeight - headSize - torsoHeight;
		leftLegBone = CreateBone("LeftLeg", skeleton, new Vector3(-limbWidth / 2, legHeight, 0));
		rightLegBone = CreateBone("RightLeg", skeleton, new Vector3(limbWidth / 2, legHeight, 0));

		GenerateHead(headBone);
		GenerateTorso(torsoBone);
		GenerateLimb("LeftArm", leftArmBone, armColor, new Vector3(0, -1, 0));
		GenerateLimb("RightArm", rightArmBone, armColor, new Vector3(0, -1, 0));
		GenerateLimb("LeftLeg", leftLegBone, legColor, new Vector3(0, -1, 0));
		GenerateLimb("RightLeg", rightLegBone, legColor, new Vector3(0, -1, 0));
	}

	Transform CreateBone(string name, Transform parent, Vector3 localPos)
	{
		GameObject bone = new GameObject(name + "_Bone");
		bone.transform.SetParent(parent);
		bone.transform.localPosition = localPos;
		return bone.transform;
	}

	void GenerateHead(Transform parent)
	{
		if (headPrefab == null)
		{
			Debug.LogWarning("Head prefab is not assigned! Skipping head generation.");
			return;
		}

		headInstance = Instantiate(headPrefab, parent);
		headInstance.name = "HeadPrefab";
		headInstance.transform.localPosition = Vector3.zero;
		headInstance.transform.localRotation = Quaternion.Euler(headPrefabRotationOffset);
		headInstance.transform.localScale = headPrefabScale;
	}

	void GenerateTorso(Transform parent)
	{
		float torsoWidth = cubeSize * 8f;
		float torsoHeight = cubeSize * 12f;
		float torsoDepth = cubeSize * 4f;

		GameObject torsoCube = CreateCube(new Vector3(torsoWidth, torsoHeight, torsoDepth), torsoColor);
		torsoCube.transform.SetParent(parent);
		torsoCube.transform.localPosition = Vector3.zero;
	}

	void GenerateLimb(string name, Transform parent, Color color, Vector3 direction)
	{
		List<GameObject> cubes = new List<GameObject>();
		List<Vector3> offsets = new List<Vector3>();

		float limbWidth = cubeSize * 4f;
		int cubeCount = 5;
		Transform previousBone = parent;

		for (int i = 0; i < cubeCount; i++)
		{
			Transform bone = CreateBone(name + "_Segment" + i, previousBone, direction * limbWidth * (i > 0 ? 1 : 0));
			GameObject cube = CreateCube(limbWidth, color);
			cube.name = name + "_Cube" + i;
			cube.transform.SetParent(bone);
			cube.transform.localPosition = Vector3.zero;

			cubes.Add(cube);
			offsets.Add(Vector3.zero);
			previousBone = bone;
		}

		bodyPartCubes[name] = cubes;
		originalCubeOffsets[name] = offsets;
		bodyPartBones[name] = null;
	}

	GameObject CreateCube(float size, Color color)
	{
		return CreateCube(new Vector3(size, size, size), color);
	}

	GameObject CreateCube(Vector3 size, Color color)
	{
		GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
		cube.transform.localScale = size;
		Material mat = new Material(Shader.Find("Standard"));
		mat.color = color;
		cube.GetComponent<Renderer>().material = mat;
		Destroy(cube.GetComponent<Collider>());
		return cube;
	}

	void SetupEZSoftBones()
	{
		string[] limbs = { "LeftArm", "RightArm", "LeftLeg", "RightLeg" };
		foreach (string limb in limbs)
		{
			Transform limbRoot = skeleton.Find(limb + "_Bone");
			if (limbRoot == null) continue;

			EZSoftBone softBone = limbRoot.gameObject.AddComponent<EZSoftBone>();
			EZSoftBoneMaterial softBoneMaterial = ScriptableObject.CreateInstance<EZSoftBoneMaterial>();
			softBoneMaterial.damping = normalDamping;
			softBoneMaterial.stiffness = normalStiffness;
			softBoneMaterial.resistance = 0f;
			softBoneMaterial.slackness = 0f;
			softBone.sharedMaterial = softBoneMaterial;

			Transform firstSegment = limbRoot.Find(limb + "_Segment0");
			if (firstSegment != null)
			{
				softBone.rootBones.Clear();
				softBone.rootBones.Add(firstSegment);
			}

			softBone.startDepth = 0;
			softBone.iterations = 2;
			softBone.sleepThreshold = 0.005f;
			softBone.deltaTimeMode = EZSoftBone.DeltaTimeMode.DeltaTime;
			softBone.gravity = Vector3.zero;
			bodyPartBones[limb] = softBone;
		}
	}

	void Update()
	{
		if (player == null) return;

		float distanceMoved = Vector3.Distance(transform.position, lastPosition);
		isMoving = distanceMoved > 0.001f;
		worldMoveSpeed = distanceMoved / Mathf.Max(0.0001f, Time.deltaTime);
		lastPosition = transform.position;

		float distance = Vector3.Distance(player.position, transform.position);
		nearBlend = 1f - Mathf.Clamp01((distance - nearGroundedDistance) / Mathf.Max(0.01f, farStylizedDistance - nearGroundedDistance));
		chaseActive = villainAI != null && villainAI.IsChasing;
		if (chaseActive)
		{
			nearBlend = Mathf.Clamp01(nearBlend + chaseRunBlendBoost);
		}

		float targetHorrorLevel = 0f;
		if (distance <= fullHorrorDistance)
		{
			targetHorrorLevel = 1f;
		}
		else if (distance <= activationDistance)
		{
			targetHorrorLevel = 1f - ((distance - fullHorrorDistance) / (activationDistance - fullHorrorDistance));
		}
		currentHorrorLevel = Mathf.Lerp(currentHorrorLevel, targetHorrorLevel, Time.deltaTime * transformSpeed);

		if (isMoving)
		{
			float speedNorm = Mathf.Clamp01(worldMoveSpeed / Mathf.Max(0.1f, runSpeedForFullBlend));
			float cadence = Mathf.Lerp(walkCadence, runCadence, Mathf.Clamp01(nearBlend * 0.8f + speedNorm * 0.6f));
			if (chaseActive)
			{
				cadence *= chaseCadenceMultiplier;
			}
			walkCycle += Time.deltaTime * cadence;
		}

		ApplyHorrorTransformation();
		ApplyCubeSeparation();
		AnimateLocomotion();
		HandleAudio();
		HandleRatSounds();
	}

	void AnimateLocomotion()
	{
		if (!isMoving) return;

		if (nearBlend < 0.45f)
		{
			AnimateStylizedWalk();
		}
		else
		{
			AnimateGroundedRun();
		}
	}

	void AnimateStylizedWalk()
	{
		float leftSwing = Mathf.Sin(walkCycle * Mathf.PI);
		float rightSwing = Mathf.Sin((walkCycle + 1f) * Mathf.PI);

		if (leftLegBone != null)
		{
			leftLegBone.localRotation = Quaternion.Euler(leftSwing * walkLegSwing, 0f, 0f);
			Vector3 p = leftLegBone.localPosition;
			p.y = leftLegBaseY + Mathf.Max(0f, leftSwing) * walkStepHeight;
			p.x = leftLegBaseX;
			p.z = 0f;
			leftLegBone.localPosition = p;
		}

		if (rightLegBone != null)
		{
			rightLegBone.localRotation = Quaternion.Euler(rightSwing * walkLegSwing, 0f, 0f);
			Vector3 p = rightLegBone.localPosition;
			p.y = rightLegBaseY + Mathf.Max(0f, rightSwing) * walkStepHeight;
			p.x = rightLegBaseX;
			p.z = 0f;
			rightLegBone.localPosition = p;
		}

		if (leftArmBone != null)
		{
			leftArmBone.localRotation = Quaternion.Euler(rightSwing * walkArmSwing, 0f, 0f);
		}
		if (rightArmBone != null)
		{
			rightArmBone.localRotation = Quaternion.Euler(leftSwing * walkArmSwing, 0f, 0f);
		}

		if (torsoBone != null)
		{
			torsoBone.localRotation = Quaternion.Euler(0f, Mathf.Sin(walkCycle * Mathf.PI * 2f) * spineRotationAmount, 0f);
		}

		AnimateHead(1f - currentHorrorLevel, currentHorrorLevel);
	}

	void AnimateGroundedRun()
	{
		float speedNorm = Mathf.Clamp01(worldMoveSpeed / Mathf.Max(0.1f, runSpeedForFullBlend));
		float intensity = Mathf.Clamp01((nearBlend * 0.65f) + (speedNorm * 0.7f));
		if (chaseActive)
		{
			intensity = Mathf.Clamp01(intensity + 0.25f);
		}
		float asym = Mathf.Sin(Time.time * 2.2f) * asymmetryAmount;

		float leftPhase = Mathf.Repeat(walkCycle, 1f);
		float rightPhase = Mathf.Repeat(walkCycle + 0.5f, 1f);

		ApplyGroundedLeg(leftLegBone, leftPhase, -1f, intensity, asym);
		ApplyGroundedLeg(rightLegBone, rightPhase, 1f, intensity, -asym);

		float leftSwing = Mathf.Sin(leftPhase * Mathf.PI * 2f);
		float rightSwing = Mathf.Sin(rightPhase * Mathf.PI * 2f);
		float armBalance = balanceArmSwing * intensity;
		if (chaseActive)
		{
			armBalance *= chaseArmBalanceMultiplier;
		}

		if (leftArmBone != null)
		{
			leftArmBone.localRotation = Quaternion.Euler((rightSwing * armBalance) + (asym * 12f), -balanceArmSpread * intensity, 8f * intensity);
		}
		if (rightArmBone != null)
		{
			rightArmBone.localRotation = Quaternion.Euler((leftSwing * armBalance) - (asym * 12f), balanceArmSpread * intensity, -8f * intensity);
		}

		if (torsoBone != null)
		{
			float lean = torsoLeanNear * intensity;
			if (chaseActive)
			{
				lean *= chaseTorsoLeanMultiplier;
			}
			float sway = Mathf.Sin(walkCycle * Mathf.PI * 2f) * torsoSwayAmount * (1f - intensity * 0.2f);
			float bend = Mathf.Sin(Time.time * 3.5f) * uncannyNearBend * intensity;
			torsoBone.localRotation = Quaternion.Euler(lean, sway + bend, bend * 0.3f);
		}

		AnimateHead(0.2f, 0.8f + currentHorrorLevel * 0.2f);
	}

	void ApplyGroundedLeg(Transform legBone, float phase, float sideSign, float intensity, float asymmetry)
	{
		if (legBone == null)
		{
			return;
		}

		float strideForward = Mathf.Sin(phase * Mathf.PI * 2f) * runStride * (0.6f + intensity * 0.7f);
		if (chaseActive)
		{
			strideForward *= chaseStrideMultiplier;
		}
		float lift = Mathf.Max(0f, Mathf.Sin((phase - 0.08f) * Mathf.PI * 2f)) * runLift * (0.55f + intensity * 0.6f);
		float bend = Mathf.Sin(Time.time * 4.4f + (sideSign > 0f ? 0.7f : 0f)) * uncannyNearBend * intensity * 0.35f;

		Vector3 localTarget = new Vector3(sideSign * cubeSize * 2f, (sideSign < 0f ? leftLegBaseY : rightLegBaseY) + lift, strideForward);
		Vector3 worldTarget = transform.TransformPoint(localTarget);
		worldTarget = ProjectToGround(worldTarget);

		Vector3 current = legBone.position;
		legBone.position = Vector3.Lerp(current, worldTarget, Time.deltaTime * ikBlendSpeed * Mathf.Lerp(0.45f, 1f, footPlantStrength));
		legBone.localRotation = Quaternion.Euler((-strideForward * 180f) + bend + asymmetry * 8f, asymmetry * 6f, 0f);
	}

	Vector3 ProjectToGround(Vector3 worldPos)
	{
		Vector3 origin = worldPos + Vector3.up * floorRayStartHeight;
		if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, floorRayLength, groundMask, QueryTriggerInteraction.Ignore))
		{
			worldPos.y = hit.point.y;
		}
		return worldPos;
	}

	void ApplyHorrorTransformation()
	{
		foreach (var kvp in bodyPartBones)
		{
			if (kvp.Value != null && kvp.Value.sharedMaterial != null)
			{
				EZSoftBoneMaterial mat = kvp.Value.material;
				mat.damping = Mathf.Lerp(normalDamping, horrorDamping, currentHorrorLevel);
				mat.stiffness = Mathf.Lerp(normalStiffness, horrorStiffness, currentHorrorLevel);
				kvp.Value.gravity = Vector3.Lerp(Vector3.zero, horrorGravity, currentHorrorLevel);
			}
		}
	}

	void ApplyCubeSeparation()
	{
		foreach (var kvp in bodyPartCubes)
		{
			List<GameObject> cubes = kvp.Value;
			List<Vector3> offsets = originalCubeOffsets[kvp.Key];
			for (int i = 0; i < cubes.Count; i++)
			{
				Vector3 separation = Random.insideUnitSphere * maxCubeSeparation * currentHorrorLevel;
				cubes[i].transform.localPosition = offsets[i] + separation;
			}
		}
	}

	void AnimateHead(float minecraftWeight, float horrorWeight)
	{
		if (headBone == null) return;

		Vector3 headPos = headBone.localPosition;
		float bobAmount = Mathf.Sin(walkCycle * Mathf.PI * 2f) * 0.05f;
		headPos.y = characterHeight - (cubeSize * 4f) + bobAmount * minecraftWeight;
		headBone.localPosition = headPos;

		Quaternion baseRotation = Quaternion.identity;
		if (currentHorrorLevel > 0.3f)
		{
			float time = Time.time;
			float wiggleX = Mathf.Sin(time * headWiggleSpeed) * headWiggleAmount * horrorWeight;
			float twitchY = Mathf.PerlinNoise(time * headTwitchSpeed, 0) * headTwitchIntensity * horrorWeight;
			float twitchZ = Mathf.PerlinNoise(0, time * headTwitchSpeed) * headTwitchIntensity * 0.5f * horrorWeight;
			if (Mathf.PerlinNoise(time * 2f, 100f) > 0.95f)
			{
				twitchY += Mathf.Sin(time * 30f) * 60f * horrorWeight;
			}
			baseRotation = Quaternion.Euler(wiggleX, twitchY - twitchY / 2, twitchZ);
		}

		headBone.localRotation = baseRotation;
		if (headInstance != null)
		{
			headInstance.transform.localRotation = Quaternion.Euler(headPrefabRotationOffset);
		}
	}

	void HandleAudio()
	{
		if (audioSource != null && horrorSound != null)
		{
			if (currentHorrorLevel > 0.5f && !audioSource.isPlaying)
			{
				audioSource.Play();
			}
			else if (currentHorrorLevel <= 0.5f && audioSource.isPlaying)
			{
				audioSource.Stop();
			}
			audioSource.volume = currentHorrorLevel;
		}
	}

	void HandleRatSounds()
	{
		if (currentHorrorLevel > 0.7f && ratSound != null && ratAudioSource != null)
		{
			if (Time.time >= nextRatSoundTime && !ratAudioSource.isPlaying)
			{
				ratAudioSource.pitch = Random.Range(0.8f, 1.3f);
				ratAudioSource.PlayOneShot(ratSound);
				nextRatSoundTime = Time.time + Random.Range(ratSoundMinInterval, ratSoundMaxInterval);
			}
		}
	}

	void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireSphere(transform.position, activationDistance);
		Gizmos.color = Color.red;
		Gizmos.DrawWireSphere(transform.position, fullHorrorDistance);
	}
}
