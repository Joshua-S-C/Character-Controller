using System;
using System.Numerics;

namespace Nyteshade.Modules.Anim
{
    public class MeshSkinner
    {
        private readonly Skeleton _skeleton;
        private readonly Matrix4x4[] _finalMatrices;

        public Matrix4x4[] FinalMatrices => _finalMatrices;

        public MeshSkinner(Skeleton skeleton)
        {
            _skeleton = skeleton ?? throw new ArgumentNullException(nameof(skeleton));
            _finalMatrices = new Matrix4x4[_skeleton.BoneCount];
        }

        public void UpdateSkinning()
        {
            for (int i = 0; i < _skeleton.BoneCount; i++)
            {
                var bone = _skeleton.GetBone(i);

                // Local-to-world matrix from FK
                var world = bone.GetLocalToWorldMatrix();

                // Inverse bind matrix (rest pose)
                var invBind = _skeleton.InverseBindMatrices[i];

                // Key skinning formula:  transforms vertex from bind → animated pose
                _finalMatrices[i] = invBind * world;
            }
        }
    }
}