using Game.Core.GM_Task;
using ProtoBuf;
using System;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

namespace Game.Datas.GMEntities
{
    [Serializable]
    public struct Vector3 {
        public float x;
        public float y;
        public float z;

        public Vector3(float x, float y, float z) { 
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static float Distance(ref Vector3 lhs, ref Vector3 rhs) { 
            return MathF.Sqrt((lhs.x - rhs.x) * (lhs.x - rhs.x) + (lhs.z - rhs.z) * (lhs.z - rhs.z) + (lhs.y - rhs.y) * (lhs.y - rhs.y));
                        
        }

        public static float Len(ref Vector3 dir)
        {
            return MathF.Sqrt((dir.x * dir.x) + (dir.y * dir.y) + (dir.z * dir.z));

        }

        public static Vector3 Sub(ref Vector3 lhs, ref Vector3 rhs)
        {
            return new Vector3(lhs.x - rhs.x, lhs.y - rhs.y, lhs.z - rhs.z);
        }

    }

    public struct Vector2 {
        public float x;
        public float y;

        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public static Vector2 Sub(ref Vector2 lhs, ref Vector2 rhs) {
            return new Vector2(lhs.x - rhs.x, lhs.y - rhs.y);
        }

        public static float Distance(ref Vector2 lhs, ref Vector2 rhs)
        {
            return MathF.Sqrt((lhs.x - rhs.x) * (lhs.x - rhs.x) + (lhs.y - rhs.y) * (lhs.y - rhs.y));

        }

        public static float Len(ref Vector2 dir)
        {
            return MathF.Sqrt((dir.x * dir.x) + (dir.y * dir.y));

        }
    }

    public struct TransformComponent
    {
        public Vector3 pos;
        public Vector3 eulerAngles;

        public static void Init(GM_PlayerEntity entity)
        {
            entity.uTransform.pos = new Vector3(0, 0, 0);
            entity.uTransform.eulerAngles = new Vector3(0, 0, 0);
        }

    }

}