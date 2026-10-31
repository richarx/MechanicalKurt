using TMPro;
using UnityEngine;

/// <summary>Texte affiché au joueur local par une MissionStartZone : décompte, attente des joueurs, course en cours.</summary>
public class MissionCountdownHud : MonoBehaviour
{
    [SerializeField] TMP_Text _text;

    public void Show(string content)
    {
        _text.text = content;
        gameObject.SetActive(true);
    }

    public void Hide() => gameObject.SetActive(false);
}
