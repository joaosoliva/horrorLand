public interface ILightInteractable
{
	string LightId { get; }
	bool IsOn { get; }
	void TriggerFlicker(float duration);
	void TurnOff(float duration = 0f);
	void DelayedActivate(float delaySeconds);
}
