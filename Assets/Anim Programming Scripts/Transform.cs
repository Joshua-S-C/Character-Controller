using System;
using System.Collections.Generic;
using System.Numerics;
using UnityEditor.Experimental.GraphView;

namespace NyteshadeGodot.Modules.Maths
{
    public class Transform
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

        public string Name { get; set; }

        private Transform _Parent = null;
        private List<Transform> _children = new();

        public Transform Parent => _Parent;
        public IReadOnlyList<Transform> Children => _children.AsReadOnly();


        public Transform()
        {
            Position = Vector3.Zero;
            Rotation = Quaternion.Identity;
            Scale = Vector3.One;
            Name = "Transform";
        }


        //Hierarchy Functions
        public void SetParent(Transform newParent)
        {
            if (_Parent != null)
            {
                _Parent._children.Remove(this);
            }

            _Parent = newParent;

            if (_Parent != null)
            {
                _Parent._children.Add(this);
            }
        }

        public Transform GetParent()
        {
            return null;
        }

        public Transform GetChild(string name)
        {
            return null;
        }

        public Transform GetChildren()
        {
            return null;
        }

        public Transform AddChild(string childName)
        {
            return null;
        }
        public Transform AddChild(Transform child, bool keepWorldTransform = false)
        {
            return null;
        }

        public Transform RemoveChild(string name, bool keepWorldTransform = false)
        {
            return null;
        }

        public Transform RemoveChild(Transform child, bool keepWorldTransform = false)
        {
            return null;
        }


        //Matrix Functions
        public Matrix4x4 GetLocalMatrix()
        {
            return Matrix4x4.CreateScale(Scale) *
                   Matrix4x4.CreateFromQuaternion(Rotation) *
                   Matrix4x4.CreateTranslation(Position);
        }
        public Matrix4x4 GetLocalToWorldMatrix()
        {
            Matrix4x4 localMatrix = GetLocalMatrix();

            if (_Parent != null)
            {
                return localMatrix * Parent.GetLocalToWorldMatrix();
            }
            else
            {
                return localMatrix;
            }
        }

        /// <returns>Nyteshade transform from a Unity Transform</returns>
        public static Transform UnityToNyteshadeTransform(UnityEngine.Transform transform)
        {
            var t = new Transform
            {
                Name = transform.name,
                Position = new Vector3(transform.transform.position.x, transform.transform.position.y, transform.transform.position.z),
                Rotation = new Quaternion(transform.transform.rotation.x, transform.transform.rotation.y, transform.transform.rotation.z, transform.transform.rotation.w),
                Scale = new Vector3(transform.transform.localScale.x, transform.transform.localScale.y, transform.transform.localScale.z)
            };

            return t;
        }
    }
}