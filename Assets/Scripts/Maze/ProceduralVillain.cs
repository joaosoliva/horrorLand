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
	[SerializeField] private float characterHeight = 1.8f; // Same as player
    
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
	[SerializeField] private AudioClip ratSound; // Rat squeaking/chittering sound
	[SerializeField] private float ratSoundMinInterval = 0.5f;
	[SerializeField] private float ratSoundMaxInterval = 2f;
    
	[Header("Procedural Animation")]
	[SerializeField] private float moveSpeed = 2f;
	[SerializeField] private float horrorMoveSpeed = 5f;
	[SerializeField] private float stepHeight = 0.2f;
	[SerializeField] private float stepFrequency = 2f;
	[SerializeField] private float armSwingAmount = 30f;
	[SerializeField] private float horrorArmSwingAmount = 60f;
	[SerializeField] private float horrorArmSwingSpeed = 5f;
	[SerializeField] private float spineRotationAmount = 5f;
    
	[Header("Horror Head Effects")]
	[SerializeField] private float headWiggleSpeed = 15f;
	[SerializeField] private float headWiggleAmount = 25f;
	[SerializeField] private float headTwitchIntensity = 45f;
	[SerializeField] private float headTwitchSpeed = 12f;
    
	// Internal references
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
    
	// Body structure
	private Transform skeleton;
	private Transform headBone;
	private Transform torsoBone;
	private Transform leftArmBone;
	private Transform rightArmBone;
	private Transform leftLegBone;
	private Transform rightLegBone;

	void Start()
	{
		audioSource = gameObject.AddComponent<AudioSource>();
		audioSource.clip = horrorSound;
		audioSource.loop = true;
		audioSource.spatialBlend = 1f;
        
		// Add second audio source for rat sounds
		ratAudioSource = gameObject.AddComponent<AudioSource>();
		ratAudioSource.clip = ratSound;
		ratAudioSource.loop = false;
		ratAudioSource.spatialBlend = 1f;
		ratAudioSource.pitch = 1f;
		ratAudioSource.volume = 2f;
        
		lastPosition = transform.position;
        
		GenerateVillain();
		SetupEZSoftBones();
	}

	void GenerateVillain()
	{
		// Create skeleton structure
		skeleton = new GameObject("Skeleton").transform;
		skeleton.SetParent(transform);
		skeleton.localPosition = Vector3.zero;
        
		// Calculate proportions based on Minecraft standards
		float headSize = cubeSize * 8f; // Head is 8x8x8
		float torsoWidth = cubeSize * 8f;
		float torsoHeight = cubeSize * 12f;
		float limbWidth = cubeSize * 4f;
        
		// Create bone hierarchy
		headBone = CreateBone("Head", skeleton, new Vector3(0, characterHeight - headSize/2, 0));
		torsoBone = CreateBone("Torso", skeleton, new Vector3(0, characterHeight - headSize - torsoHeight/2, 0));
        
		float armHeight = characterHeight - headSize - cubeSize * 2f;
		leftArmBone = CreateBone("LeftArm", skeleton, new Vector3(-torsoWidth/2 - limbWidth/2, armHeight, 0));
		rightArmBone = CreateBone("RightArm", skeleton, new Vector3(torsoWidth/2 + limbWidth/2, armHeight, 0));
        
		float legHeight = characterHeight - headSize - torsoHeight;
		leftLegBone = CreateBone("LeftLeg", skeleton, new Vector3(-limbWidth/2, legHeight, 0));
		rightLegBone = CreateBone("RightLeg", skeleton, new Vector3(limbWidth/2, legHeight, 0));
        
		// Generate body parts
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
        
		// Instantiate the head prefab
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
        
		// Torso as single piece (no soft bone)
		GameObject torsoCube = CreateCube(new Vector3(torsoWidth, torsoHeight, torsoDepth), torsoColor);
		torsoCube.transform.SetParent(parent);
		torsoCube.transform.localPosition = Vector3.zero;
	}
    
	void GenerateLimb(string name, Transform parent, Color color, Vector3 direction)
	{
		List<GameObject> cubes = new List<GameObject>();
		List<Vector3> offsets = new List<Vector3>();
		List<Transform> bones = new List<Transform>();
        
		float limbWidth = cubeSize * 4f;
		int cubeCount = 5;
        
		Transform previousBone = parent;
        
		for (int i = 0; i < cubeCount; i++)
		{
			// Create bone
			Transform bone = CreateBone(name + "_Segment" + i, previousBone, direction * limbWidth * (i > 0 ? 1 : 0));
			bones.Add(bone);
            
			// Create cube
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
        
		// Store first bone for EZSoftBone
		bodyPartBones[name] = null; // Will be set up in SetupEZSoftBones
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
        
		// Remove collider for now (add back if needed)
		Destroy(cube.GetComponent<Collider>());
        
		return cube;
	}
    
	void SetupEZSoftBones()
	{
		// Add EZSoftBone components to limbs
		string[] limbs = { "LeftArm", "RightArm", "LeftLeg", "RightLeg" };
        
		foreach (string limb in limbs)
		{
			Transform limbRoot = skeleton.Find(limb + "_Bone");
			if (limbRoot == null) continue;
            
			// Add EZSoftBone component to the limb root bone
			EZSoftBone softBone = limbRoot.gameObject.AddComponent<EZSoftBone>();
            
			// Create material using CreateInstance
			EZSoftBoneMaterial softBoneMaterial = ScriptableObject.CreateInstance<EZSoftBoneMaterial>();
            
			// Set initial material properties
			softBoneMaterial.damping = normalDamping;
			softBoneMaterial.stiffness = normalStiffness;
			softBoneMaterial.resistance = 0f;
			softBoneMaterial.slackness = 0f;
            
			// Assign material to the soft bone
			softBone.sharedMaterial = softBoneMaterial;
            
			// Set root bones - need to find the first segment and add to the list
			Transform firstSegment = limbRoot.Find(limb + "_Segment0");
			if (firstSegment != null)
			{
				softBone.rootBones.Clear();
				softBone.rootBones.Add(firstSegment);
			}
            
			// Configure simulation settings
			softBone.startDepth = 0;
			softBone.iterations = 2;
			softBone.sleepThreshold = 0.005f;
			softBone.deltaTimeMode = EZSoftBone.DeltaTimeMode.DeltaTime;
            
			// Set gravity directly on the EZSoftBone component (not the material)
			softBone.gravity = Vector3.zero;
            
			// Store reference
			bodyPartBones[limb] = softBone;
		}
	}

	void Update()
	{
		if (player == null) return;
        
		// Check if moving
		float distanceMoved = Vector3.Distance(transform.position, lastPosition);
		isMoving = distanceMoved > 0.001f;
		lastPosition = transform.position;
        
		float distance = Vector3.Distance(player.position, transform.position);
        
		// Calculate horror level
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
        
		// Update walk cycle
		if (isMoving)
		{
			float currentSpeed = Mathf.Lerp(moveSpeed, horrorMoveSpeed, currentHorrorLevel);
			walkCycle += Time.deltaTime * stepFrequency * currentSpeed;
		}
        
		// Apply transformations
		ApplyHorrorTransformation();
		ApplyCubeSeparation();
		AnimateWalk();
		HandleAudio();
		HandleRatSounds();
	}
    
	void ApplyHorrorTransformation()
	{
		foreach (var kvp in bodyPartBones)
		{
			if (kvp.Value != null && kvp.Value.sharedMaterial != null)
			{
				// Get the material (this creates an instance if using .material property)
				EZSoftBoneMaterial mat = kvp.Value.material;
                
				// Update material properties
				mat.damping = Mathf.Lerp(normalDamping, horrorDamping, currentHorrorLevel);
				mat.stiffness = Mathf.Lerp(normalStiffness, horrorStiffness, currentHorrorLevel);
                
				// Set gravity based on horror level
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
				// Add random offset between cubes
				Vector3 separation = Random.insideUnitSphere * maxCubeSeparation * currentHorrorLevel;
				cubes[i].transform.localPosition = offsets[i] + separation;
			}
		}
	}
    
	void AnimateWalk()
	{
		if (!isMoving) return;
        
		// Blend between Minecraft-style and organic horror animation
		float minecraftWeight = 1f - currentHorrorLevel;
		float horrorWeight = currentHorrorLevel;
        
		// Calculate leg positions
		float leftLegSwing = Mathf.Sin(walkCycle * Mathf.PI);
		float rightLegSwing = Mathf.Sin((walkCycle + 1f) * Mathf.PI);
        
		// Minecraft-style walk (stiff, robotic)
		if (leftLegBone != null)
		{
			float minecraftAngle = leftLegSwing * armSwingAmount;
			float horrorAngle = leftLegSwing * horrorArmSwingAmount * Mathf.Sin(walkCycle * horrorArmSwingSpeed);
			float finalAngle = Mathf.Lerp(minecraftAngle, horrorAngle, currentHorrorLevel);
            
			leftLegBone.localRotation = Quaternion.Euler(finalAngle, 0, 0);
            
			// Add step height
			Vector3 legPos = leftLegBone.localPosition;
			float stepOffset = Mathf.Max(0, Mathf.Sin(walkCycle * Mathf.PI)) * stepHeight;
			legPos.y = characterHeight - (cubeSize * 8f) - (cubeSize * 12f) - (cubeSize * 4f) + stepOffset * minecraftWeight;
			leftLegBone.localPosition = legPos;
		}
        
		if (rightLegBone != null)
		{
			float minecraftAngle = rightLegSwing * armSwingAmount;
			float horrorAngle = rightLegSwing * horrorArmSwingAmount * Mathf.Sin((walkCycle + 1f) * horrorArmSwingSpeed);
			float finalAngle = Mathf.Lerp(minecraftAngle, horrorAngle, currentHorrorLevel);
            
			rightLegBone.localRotation = Quaternion.Euler(finalAngle, 0, 0);
            
			// Add step height
			Vector3 legPos = rightLegBone.localPosition;
			float stepOffset = Mathf.Max(0, Mathf.Sin((walkCycle + 1f) * Mathf.PI)) * stepHeight;
			legPos.y = characterHeight - (cubeSize * 8f) - (cubeSize * 12f) - (cubeSize * 4f) + stepOffset * minecraftWeight;
			rightLegBone.localPosition = legPos;
		}
        
		// Arm swing (opposite to legs)
		if (leftArmBone != null)
		{
			float minecraftAngle = rightLegSwing * armSwingAmount * 0.5f;
			float horrorAngle = Mathf.Sin(walkCycle * horrorArmSwingSpeed * 1.5f) * horrorArmSwingAmount;
			float finalAngle = Mathf.Lerp(minecraftAngle, horrorAngle, currentHorrorLevel);
            
			leftArmBone.localRotation = Quaternion.Euler(finalAngle, 0, 0);
		}
        
		if (rightArmBone != null)
		{
			float minecraftAngle = leftLegSwing * armSwingAmount * 0.5f;
			float horrorAngle = Mathf.Sin((walkCycle + 0.5f) * horrorArmSwingSpeed * 1.5f) * horrorArmSwingAmount;
			float finalAngle = Mathf.Lerp(minecraftAngle, horrorAngle, currentHorrorLevel);
            
			rightArmBone.localRotation = Quaternion.Euler(finalAngle, 0, 0);
		}
        
		// Torso rotation for more organic movement in horror mode
		if (torsoBone != null)
		{
			float torsoSway = Mathf.Sin(walkCycle * Mathf.PI * 2f) * spineRotationAmount * horrorWeight;
			torsoBone.localRotation = Quaternion.Euler(0, torsoSway, 0);
		}
        
		// Head animation
		AnimateHead(minecraftWeight, horrorWeight);
	}
    
	void AnimateHead(float minecraftWeight, float horrorWeight)
	{
		if (headBone == null) return;
        
		// Head bob
		Vector3 headPos = headBone.localPosition;
		float bobAmount = Mathf.Sin(walkCycle * Mathf.PI * 2f) * 0.05f;
		headPos.y = characterHeight - (cubeSize * 4f) + bobAmount * minecraftWeight;
		headBone.localPosition = headPos;
        
		// Base rotation - make sure head follows body
		Quaternion baseRotation = Quaternion.identity;
        
		// In horror mode, add freaky effects
		if (currentHorrorLevel > 0.3f)
		{
			float time = Time.time;
            
			// Rapid side-to-side wiggle (rat-like head movement)
			float wiggleX = Mathf.Sin(time * headWiggleSpeed) * headWiggleAmount * horrorWeight;
            
			// Random twitching
			float twitchY = Mathf.PerlinNoise(time * headTwitchSpeed, 0) * headTwitchIntensity * horrorWeight;
			float twitchZ = Mathf.PerlinNoise(0, time * headTwitchSpeed) * headTwitchIntensity * 0.5f * horrorWeight;
            
			// Occasional violent snap
			float snapIntensity = Mathf.PerlinNoise(time * 2f, 100f);
			if (snapIntensity > 0.95f)
			{
				twitchY += Mathf.Sin(time * 30f) * 60f * horrorWeight;
			}
            
			// Combine all rotations
			baseRotation = Quaternion.Euler(wiggleX, twitchY - twitchY/2, twitchZ);
		}
        
		// Apply rotation to head bone
		headBone.localRotation = baseRotation;
        
		// Also apply rotation directly to head instance to ensure it follows
		if (headInstance != null)
		{
			// Reset local rotation first, then apply offset
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
		// Only play rat sounds when in high horror state
		if (currentHorrorLevel > 0.7f && ratSound != null && ratAudioSource != null)
		{
			if (Time.time >= nextRatSoundTime && !ratAudioSource.isPlaying)
			{
				// Random pitch variation for more variety
				ratAudioSource.pitch = Random.Range(0.8f, 1.3f);
				ratAudioSource.PlayOneShot(ratSound);
                
				// Schedule next rat sound
				float interval = Random.Range(ratSoundMinInterval, ratSoundMaxInterval);
				nextRatSoundTime = Time.time + interval;
			}
		}
	}
    
	void OnDrawGizmosSelected()
	{
		// Visualize activation distances
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireSphere(transform.position, activationDistance);
        
		Gizmos.color = Color.red;
		Gizmos.DrawWireSphere(transform.position, fullHorrorDistance);
	}
}