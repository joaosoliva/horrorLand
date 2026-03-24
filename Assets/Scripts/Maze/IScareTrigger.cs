using UnityEngine;

public interface IScareTrigger
{
	string TriggerId { get; }
	bool IsActive { get; }
	Vector3 Position { get; }
	void ActivateScare(EnvironmentScareController controller, ScareType scareType);
}
