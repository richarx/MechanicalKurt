using System;
using UnityEngine;

public class PigeonHeightIndicator : MonoBehaviour
{
    [SerializeField] private float minHeightRequired;
    [SerializeField] private float maxHeightRequired;
    [SerializeField] private float scrollSpeed;

    [Space]
    [SerializeField] private Transform pivot;
    [SerializeField] private Vector3 offset;

    [Space]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Transform groundIndicatorPrefab;

    [Space]
    [SerializeField] private LayerMask groundLayers;

    private PigeonController pigeonController;

    private bool isDisplayed;
    private float currentDistanceFromGround;
    private float targetDistanceFromGround;
    private float lineVelocity;
    private Transform groundIndicator;

    private Vector3 startingPosition => pivot.position + offset;

    private void Start()
    {
        pigeonController = GetComponent<PigeonController>();
    }

    private void LateUpdate()
    {
        bool shouldBeDisplayed = ComputeDisplayState();

        if (shouldBeDisplayed != isDisplayed)
        {
            isDisplayed = shouldBeDisplayed;

            if (isDisplayed)
                DisplayIndicator();
            else
                HideIndicator();

            return;
        }

        if (isDisplayed)
            UpdateIndicator();
    }

    private void UpdateIndicator()
    {
        currentDistanceFromGround = Mathf.SmoothDamp(currentDistanceFromGround, targetDistanceFromGround, ref lineVelocity, 0.1f);
        lineRenderer.SetPosition(0, startingPosition);
        lineRenderer.SetPosition(1, startingPosition + Vector3.down * currentDistanceFromGround);

        if (groundIndicator != null)
            groundIndicator.position = startingPosition + Vector3.down * targetDistanceFromGround;
    }

    private void DisplayIndicator()
    {
        currentDistanceFromGround = 0.0f;

        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, startingPosition);
        lineRenderer.SetPosition(1, startingPosition + Vector3.down * currentDistanceFromGround);

        groundIndicator = Instantiate(groundIndicatorPrefab);
        groundIndicator.position = startingPosition + Vector3.down * targetDistanceFromGround;
    }

    private void HideIndicator()
    {
        lineRenderer.positionCount = 0;
        if (groundIndicator != null)
            Destroy(groundIndicator.gameObject);
    }

    private bool ComputeDisplayState()
    {
        if (pigeonController.CurrentPigeonState != PigeonController.MovementState.Braking)
            return false;

        float distance = ComputeDistanceToGround();

        if (distance < minHeightRequired || distance < 0.0f)
            return false;

        targetDistanceFromGround = distance;

        return true;
    }

    private float ComputeDistanceToGround()
    {
        RaycastHit hit;
        bool isHit = Physics.Raycast(transform.position, Vector3.down, out hit, maxHeightRequired, groundLayers);

        if (!isHit)
            return -1.0f;

        return hit.distance;
    }
}
