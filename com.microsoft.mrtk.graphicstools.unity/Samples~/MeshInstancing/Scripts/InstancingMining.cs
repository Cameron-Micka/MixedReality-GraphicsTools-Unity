// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Microsoft.MixedReality.GraphicsTools.Samples.MeshInstancing
{
    /// <summary>
    /// TODO
    /// </summary>
    public class InstancingMining : MonoBehaviour
    {
        [SerializeField]
        private MeshInstancer instancer = null;

        [Header("Simulation Properties")]
        [SerializeField, Min(1)]
        private int dimension = 10;

        private bool didStart = false;
        private MeshInstancer.RaycastHit lastRaycastHit;
        private Color lastColor;

        /// <summary>
        /// Re-spawn instances when a property changes.
        /// </summary>
        private void OnValidate()
        {
            if (didStart)
            {
                CreateInstances();
            }
        }

        /// <summary>
        /// Create instances on start.
        /// </summary>
        private void Start()
        {
            CreateInstances();
            didStart = true;
        }

        private void Update()
        {
            if (instancer.RaycastInstances)
            {
                // Default to the camera look position.
                Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);

#if ENABLE_INPUT_SYSTEM
                if (Mouse.current != null)
                {
                    Vector2 mousePosition2D = Mouse.current.position.ReadValue();
                    Vector3 mousePosition = new Vector3(mousePosition2D.x, mousePosition2D.y, Camera.main.nearClipPlane);
                    ray.origin = Camera.main.ScreenToWorldPoint(mousePosition);
                    ray.direction = (Camera.main.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y, Camera.main.farClipPlane)) - ray.origin).normalized;
                }
#else
                if (Input.mousePresent)
                {
                    Vector3 mousePosition = new Vector3(Input.mousePosition.x, Input.mousePosition.y, Camera.main.nearClipPlane);
                    ray.origin = Camera.main.ScreenToWorldPoint(mousePosition);
                    ray.direction = (Camera.main.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y, Camera.main.farClipPlane)) - ray.origin).normalized;
                }
#endif

                // Update the ray each frame.
                instancer.RayCollider = ray;

                // Visualize the ray and hits.
                Debug.DrawLine(instancer.RayCollider.origin, instancer.RayCollider.origin + instancer.RayCollider.direction * 100.0f, Color.red);
                int colorID = Shader.PropertyToID("_Color");

                // Clear the color on the last raycast hit,
                if (lastRaycastHit.Instance != null)
                {
                    lastRaycastHit.Instance.SetVector(colorID, lastColor);
                }

                // Color the hit as red.
                if (instancer.GetClosestRaycastHit(ref lastRaycastHit))
                {
                    Debug.DrawLine(lastRaycastHit.Point, lastRaycastHit.Point + lastRaycastHit.Direction, Color.blue);
                    lastColor = lastRaycastHit.Instance.GetVector(colorID);
                    lastRaycastHit.Instance.SetVector(colorID, Color.red);

                    // DEBUG
                    if (Mouse.current.leftButton.isPressed)
                    {
                        lastRaycastHit.Instance.Destroy();
                        lastRaycastHit.Instance = null;
                    }
                }
            }
        }

        /// <summary>
        /// Create a bunch of random point masses.
        /// </summary>
        private void CreateInstances()
        {
            // Clear any existing instances.
            instancer = (instancer == null) ? GetComponent<MeshInstancer>() : instancer;
            instancer.Clear();

            int colorID = Shader.PropertyToID("_Color");

            for (int i = 0; i < dimension; ++i)
            {
                for (int j = 0; j < dimension; ++j)
                {
                    for (int k = 0; k < dimension; ++k)
                    {
                        MeshInstancer.Instance instance = instancer.Instantiate(new Vector3(i, j, k), Quaternion.identity, Vector3.one);

                        // Set the instance color.
                        instance.SetVector(colorID, new Color((float)i / dimension, (float)j / dimension, (float)k / dimension, 1.0f));
                    }
                }
            }
        }
    }
}