using UnityEngine;

[CreateAssetMenu(fileName = "Achievement", menuName = "Achievements/Achievement")]
public sealed class AchievementData : ScriptableObject
{
    [Tooltip("Unique key used for saving. Don't change it once shipped.")]
    [SerializeField] private string _id;
    [SerializeField] private string _title;
    [SerializeField, TextArea] private string _description;
    [SerializeField] private Sprite _icon;

    public string Id => string.IsNullOrEmpty(_id) ? name : _id;
    public string Title => _title;
    public string Description => _description;
    public Sprite Icon => _icon;
}
