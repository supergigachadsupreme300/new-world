using UnityEngine;

namespace CountryLife.Helpers
{
    public static class PickupVisualHelper
    {
        public static GameObject CreatePickupCube(Transform parent, Vector3 localPosition, Vector3 localScale, Color color, bool removeCollider = true)
        {
            return CreatePickupCube(parent, localPosition, localScale, Vector3.zero, color, removeCollider);
        }

        public static GameObject CreatePickupCube(Transform parent, Vector3 localPosition, Vector3 localScale, Vector3 rotate, Color color, bool removeCollider = true)
        {
            return CreatePickupPrimitive(PrimitiveType.Cube, parent, localPosition, localScale, rotate, color, removeCollider);
        }

        public static GameObject CreatePickupSphere(Transform parent, Vector3 localPosition, float diameter, Color color, bool removeCollider = true)
        {
            return CreatePickupSphere(parent, localPosition, Vector3.zero, diameter, color, removeCollider);
        }

        public static GameObject CreatePickupSphere(Transform parent, Vector3 localPosition, Vector3 rotate, float diameter, Color color, bool removeCollider = true)
        {
            return CreatePickupPrimitive(PrimitiveType.Sphere, parent, localPosition, Vector3.one * diameter, rotate, color, removeCollider);
        }

        public static GameObject CreatePickupCylinder(Transform parent, Vector3 localPosition, Vector3 localScale, Color color, bool removeCollider = true)
        {
            return CreatePickupCylinder(parent, localPosition, localScale, Vector3.zero, color, removeCollider);
        }

        public static GameObject CreatePickupCylinder(Transform parent, Vector3 localPosition, Vector3 localScale, Vector3 rotate, Color color, bool removeCollider = true)
        {
            return CreatePickupPrimitive(PrimitiveType.Cylinder, parent, localPosition, localScale, rotate, color, removeCollider);
        }

        public static GameObject CreatePickupCapsule(Transform parent, Vector3 localPosition, Vector3 localScale, Color color, bool removeCollider = true)
        {
            return CreatePickupCapsule(parent, localPosition, localScale, Vector3.zero, color, removeCollider);
        }

        public static GameObject CreatePickupCapsule(Transform parent, Vector3 localPosition, Vector3 localScale, Vector3 rotate, Color color, bool removeCollider = true)
        {
            return CreatePickupPrimitive(PrimitiveType.Capsule, parent, localPosition, localScale, rotate, color, removeCollider);
        }

        public static GameObject CreatePickupPyramid(Transform parent, Vector3 localPosition, Vector3 localScale, Color color)
        {
            return CreatePickupPyramid(parent, localPosition, localScale, Vector3.zero, color);
        }

        public static GameObject CreatePickupPyramid(Transform parent, Vector3 localPosition, Vector3 localScale, Vector3 rotate, Color color)
        {
            var pyramid = new GameObject("Pickup_Pyramid");
            pyramid.transform.SetParent(parent, false);
            pyramid.transform.localPosition = localPosition;
            pyramid.transform.localRotation = Quaternion.Euler(rotate);
            pyramid.transform.localScale = localScale;

            var meshFilter = pyramid.AddComponent<MeshFilter>();
            var meshRenderer = pyramid.AddComponent<MeshRenderer>();
            meshRenderer.material = new Material(Shader.Find("Standard"));
            meshRenderer.material.color = color;
            meshFilter.mesh = CreatePyramidMesh();

            return pyramid;
        }

        public static GameObject CreatePickupPrimitive(PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Color color, bool removeCollider = true)
        {
            return CreatePickupPrimitive(type, parent, localPosition, localScale, Vector3.zero, color, removeCollider);
        }

        public static GameObject CreatePickupPrimitive(PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Vector3 rotate, Color color, bool removeCollider = true)
        {
            var part = GameObject.CreatePrimitive(type);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localRotation = Quaternion.Euler(rotate);
            var renderer = part.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = color;

            if (removeCollider)
            {
                var collider = part.GetComponent<Collider>();
                if (collider != null)
                    Object.Destroy(collider);
            }

            return part;
        }

        private static Mesh CreatePyramidMesh()
        {
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, 0.5f),
                new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0f, 1f, 0f)
            };
            mesh.triangles = new[]
            {
                0, 1, 4,
                1, 2, 4,
                2, 3, 4,
                3, 0, 4,
                3, 1, 0,
                3, 2, 1
            };
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
