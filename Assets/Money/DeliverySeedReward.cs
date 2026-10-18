using UnityEngine;

[DisallowMultipleComponent]
public sealed class DeliverySeedReward : MonoBehaviour
{
    [SerializeField] private SeedReward _seedReward;

    // Connect DeliveryManager.OnDelivered using the Dynamic DeliveryOffer entry.
    public void RewardDelivery(DeliveryOffer offer)
    {
        if (!isActiveAndEnabled)
            return;

        if (offer == null || offer.Destination == null)
        {
            Debug.LogWarning("Delivery reward needs an offer with a destination.", this);
            return;
        }

        SeedReward reward = _seedReward != null ? _seedReward : SeedReward.instance;
        if (reward == null || !reward.isActiveAndEnabled || ScoreManager.instance == null)
        {
            Debug.LogWarning("Delivery reward needs an active SeedReward and a ScoreManager in the scene.", this);
            return;
        }

        reward.GetReward(offer.Destination.Position, offer.Reward);
    }
}
