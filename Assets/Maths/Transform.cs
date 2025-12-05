using System;
using System.Collections.Generic;
using System.Numerics;

namespace NyteshadeGodot.Modules.Maths;

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
            // [FIX] Correct matrix multiplication order is Child * Parent
            return localMatrix * Parent.GetLocalToWorldMatrix(); 
        }
        else
        {
            // We are the root, so our local matrix IS our world matrix
            return localMatrix;
        }
    }
}