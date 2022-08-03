// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using UnityEngine;

namespace Microsoft.MixedReality.GraphicsTools.Samples.MeshInstancing
{
    /// <summary>
    /// Simulates a bunch of point masses within a containment radius.
    /// </summary>
    public class InstancingPointMass : MonoBehaviour
    {
        [SerializeField]
        private MeshInstancer instancer = null;

        [Header("Simulation Properties")]
        [SerializeField, Min(0)]
        private float containmentRadius = 3.0f;

        [Header("Instance Properties")]
        [SerializeField, Min(1)]
        private int instanceCount = 20000;
        [SerializeField, Range(0.01f, 1.0f)]
        private float instanceSizeMin = 0.02f;
        [SerializeField, Range(0.01f, 1.0f)]
        private float instanceSizeMax = 0.08f;

        private bool didStart = false;
        private Quaternion rotate90 = Quaternion.AngleAxis(90.0f, Vector3.right);

        private class PointMassData
        {
            public Vector3 velocity;
            public bool reflecting;
        }
        
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

        /// <summary>
        ///  Render the containment radius.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireSphere(Vector3.zero, containmentRadius);
        }

        /// <summary>
        /// Create a bunch of random point masses.
        /// </summary>
        private void CreateInstances()
        {
            // Clear any existing instances.
            instancer = (instancer == null) ? GetComponent<MeshInstancer>() : instancer;
            instancer.Clear();

            float minMass = instanceSizeMin * instanceSizeMin * instanceSizeMin;
            float maxMass = instanceSizeMax * instanceSizeMax * instanceSizeMax;
            int colorID = Shader.PropertyToID("_Color");

            for (int i = 0; i < instanceCount; ++i)
            {
                // Create a random scale/mass.
                Vector3 scale = Vector3.one * Random.Range(instanceSizeMin, instanceSizeMax);
                float normalizedMass = ((scale.x * scale.y * scale.z) - minMass) / maxMass;
                Vector3 velocity = Random.onUnitSphere * ((1.0f - normalizedMass) * 0.2f);

                // Create an instance object at a random position within the containment radius.
                MeshInstancer.Instance instance = instancer.Instantiate(Random.insideUnitSphere * containmentRadius, 
                                                                        Quaternion.LookRotation(velocity) * rotate90, 
                                                                        scale);
                
                // Set the instance color.
                instance.SetVector(colorID, Color.HSVToRGB(Mathf.Lerp(1.0f, 0.6f, normalizedMass), 1.0f, 0.5f));

                // Set user data to use during update.
                instance.UserData = new PointMassData()
                {
                    velocity = velocity
                };

                instance.SetParallelUpdate(ParallelUpdate);
            }
        }

        /// <summary>
        /// Method called potentially concurrently across many threads. Make sure all function calls are thread safe.
        /// </summary>
        private void ParallelUpdate(float deltaTime, MeshInstancer.Instance instance)
        {
            PointMassData data = (PointMassData)instance.UserData;

            // Euler integration.
            instance.LocalPosition += data.velocity * deltaTime;

            // Check if outside the containment radius.
            if (instance.LocalPosition.sqrMagnitude > (containmentRadius * containmentRadius))
            {
                if (!data.reflecting)
                {
                    // If outside the radius and not already reflecting then reflect off the containment sphere.
                    Vector3 sphereNormal = (Vector3.zero - instance.LocalPosition).normalized;
                    data.velocity = Vector3.Reflect(data.velocity, sphereNormal);
                    instance.LocalRotation = Quaternion.LookRotation(data.velocity) * rotate90;

                    data.reflecting = true;
                }
            }
            else
            {
                data.reflecting = false;
            }

            // Update the user data.
            instance.UserData = data;
        }

    }
}