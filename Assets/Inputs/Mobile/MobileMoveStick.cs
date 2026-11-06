using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;

/// <summary>
/// Remplit le stick gauche de la manette virtuelle : avec l'inclinaison du téléphone, ou avec le joystick à l'écran
/// quand il est à l'écran et tenu. Le jeu reçoit donc les mêmes commandes qu'avec une manette.
/// </summary>
public class MobileMoveStick : OnScreenControl, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [InputControl(layout = "Vector2")]
    [SerializeField] string _controlPath = "<Gamepad>/leftStick";

    protected override string controlPathInternal
    {
        get => _controlPath;
        set => _controlPath = value;
    }

    RectTransform _area;
    RectTransform _knob;
    float _range;
    bool _dragging;
    Vector2 _dragValue;
    Vector2 _lastSent = new Vector2(float.NaN, float.NaN);

    public Vector2 CurrentValue => _lastSent;

    public void Setup(RectTransform area, RectTransform knob, float range)
    {
        _area = area;
        _knob = knob;
        _range = range;
    }

    void Update()
    {
        Vector2 value = _dragging ? _dragValue : TiltBridge.Stick;
        if (value != _lastSent)
        {
            _lastSent = value;
            SendValueToControl(value);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _dragging = true;
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return;
        Vector2 offset = Vector2.ClampMagnitude(local, _range);
        _dragValue = offset / _range;
        if (_knob != null)
            _knob.anchoredPosition = offset;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _dragging = false;
        _dragValue = Vector2.zero;
        if (_knob != null)
            _knob.anchoredPosition = Vector2.zero;
    }

    protected override void OnDisable()
    {
        _dragging = false;
        _lastSent = new Vector2(float.NaN, float.NaN);
        base.OnDisable();
    }
}
