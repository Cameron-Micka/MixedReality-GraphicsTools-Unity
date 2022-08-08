// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.GraphicsTools.Samples.MeshInstancing
{
    /// <summary>
    /// Places instances randomly on the triangles of a mesh.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public class InstancingPlaceOnMesh : MonoBehaviour
    {
        [SerializeField]
        private MeshInstancer instancer = null;
        [SerializeField, Min(1)]
        private int instanceCount = 20000;
        [SerializeField]
        private float instanceScale = 1.0f;

        private static Quaternion rotate90 = Quaternion.AngleAxis(90.0f, Vector3.right);

        private class RocketData
        {
            public float launchTime;
            public Vector3 acceleration;
            public Vector3 velocity;
        }

        /// <summary>
        /// Re-spawn instances when a property changes.
        /// </summary>
        private void OnValidate()
        {
            if (instancer != null && instancer.InstanceCount != 0)
            {
                CreateInstances();
            }
        }

        /// <summary>
        /// Create instances on enable.
        /// </summary>
        private void OnEnable()
        {
            CreateInstances();
        }

        /// <summary>
        /// Queries a mesh for its triangles and weights them based on area.
        /// </summary>
        private void CreateInstances()
        {
            // Clear any existing instances.
            instancer = (instancer == null) ? GetComponent<MeshInstancer>() : instancer;
            instancer.Clear();

            // Get the verticies and indicies from the current mesh filter.
            MeshFilter meshFilter = GetComponent<MeshFilter>();

            if (meshFilter == null)
            {
                return;
            }

            UnityEngine.Mesh mesh = meshFilter.sharedMesh;

            if (mesh == null)
            {
                return;
            }

            Vector3[] verticies = mesh.vertices;
            int[] indicies = mesh.triangles;

            // Turn the verticies and indicies into triangles and areas.
            int triangleCount = indicies.Length / 3;
            Vector3[,] triangles = new Vector3[triangleCount, 3];
            float[] areas = new float[triangleCount];
            Vector3[] normals = new Vector3[triangleCount];

            for (int i = 0; i < indicies.Length; i += 3)
            {
                int index = i / 3;
                triangles[index, 0] = verticies[indicies[i + 0]];
                triangles[index, 1] = verticies[indicies[i + 1]];
                triangles[index, 2] = verticies[indicies[i + 2]];

                // Area of a triangle is half of the cross product's magnitude.
                Vector3 a = triangles[index, 1] - triangles[index, 0];
                Vector3 b = triangles[index, 2] - triangles[index, 1];
                Vector3 cross = Vector3.Cross(a, b);
                areas[index] = cross.magnitude * 0.5f;

                normals[index] = cross.normalized;
            }

            // Accumulate the area of the mesh.
            float meshArea = 0.0f;

            for (int i = 0; i < areas.Length; ++i)
            {
                meshArea += areas[i];
            }

            // The mesh has no area.
            if (meshArea <= 0.0f)
            {
                return;
            }

            int colorID = Shader.PropertyToID("_Color");

            for (int i = 0; i < instanceCount; ++i)
            {
                int index;
                Vector3[] triangle = PickRandomTriangle(triangles, areas, meshArea, out index);
                Vector3 position = PickRandomPointOnTriangle(triangle);

                var instance = instancer.Instantiate(position, Quaternion.LookRotation(normals[index]) * rotate90, Vector3.one * instanceScale);

                instance.SetVector(colorID, Random.ColorHSV());

                // Set the data to use during update.
                instance.UserData = new RocketData()
                {
                    launchTime = Random.Range(2.0f, 20.0f),
                    acceleration = normals[index] * 0.001f,
                    velocity = normals[index] * 0.01f
                };

                instance.SetParallelUpdate(ParallelUpdate);
            }
        }

        /// <summary>
        /// Picks a random triangle weighted by area.
        /// </summary>
        private static Vector3[] PickRandomTriangle(Vector3[,] triangles, float[] areas, float meshArea, out int index)
        {
            var random = Random.Range(0.0f, meshArea);

            for (int i = 0; i < triangles.Length; ++i)
            {
                if (random < areas[i])
                {
                    index = i;
                    return new Vector3[] { triangles[index, 0], triangles[index, 1], triangles[index, 2] };
                }
                random -= areas[i];
            }

            index = triangles.Length - 1;
            return new Vector3[] { triangles[index, 0], triangles[index, 1], triangles[index, 2] };
        }

        /// <summary>
        /// Picks a random 3D point on a triangle.
        /// </summary>
        private static Vector3 PickRandomPointOnTriangle(Vector3[] triangle)
        {
            float r1 = Mathf.Sqrt(Random.Range(0.0f, 1.0f));
            float r2 = Random.Range(0.0f, 1.0f);
            float m1 = 1 - r1;
            float m2 = r1 * (1.0f - r2);
            float m3 = r2 * r1;

            Vector3 a = triangle[0];
            Vector3 b = triangle[1];
            Vector3 c = triangle[2];

            return (m1 * a) + (m2 * b) + (m3 * c);
        }

        /// <summary>
        /// Method called potentially concurrently across many threads. Make sure all function calls are thread safe.
        /// </summary>
        private void ParallelUpdate(float deltaTime, MeshInstancer.Instance instance)
        {
            // Cast the user data to our data type.
            RocketData data = (RocketData)instance.UserData;

            data.launchTime -= deltaTime;

            if (data.launchTime <= 0.0f)
            {
                // Euler integration.
                data.velocity += data.acceleration * deltaTime;
                instance.LocalPosition += data.velocity * deltaTime;

                // Update the user data state.
                instance.UserData = data;
            }
            else
            {
                // Update the user data state.
                instance.UserData = data;
            }
        }
    }
}
