﻿using System;
using UnityEngine;

namespace UnityEditor.ShaderGraph
{
    static class ShaderValueTypeUtil
    {
        public static SlotValueType ToSlotValueType(this ConcreteSlotValueType concreteValueType)
        {
            switch(concreteValueType)
            {
                case ConcreteSlotValueType.SamplerState:
                    return SlotValueType.SamplerState;
                case ConcreteSlotValueType.Matrix4:
                    return SlotValueType.Matrix4;
                case ConcreteSlotValueType.Matrix3:
                    return SlotValueType.Matrix3;
                case ConcreteSlotValueType.Matrix2:
                    return SlotValueType.Matrix2;
                case ConcreteSlotValueType.Texture2D:
                    return SlotValueType.Texture2D;
                case ConcreteSlotValueType.Texture2DArray:
                    return SlotValueType.Texture2DArray;
                case ConcreteSlotValueType.Texture3D:
                    return SlotValueType.Texture3D;
                case ConcreteSlotValueType.Cubemap:
                    return SlotValueType.Cubemap;
                case ConcreteSlotValueType.Gradient:
                    return SlotValueType.Gradient;
                case ConcreteSlotValueType.Vector4:
                    return SlotValueType.Vector4;
                case ConcreteSlotValueType.Vector3:
                    return SlotValueType.Vector3;
                case ConcreteSlotValueType.Vector2:
                    return SlotValueType.Vector2;
                case ConcreteSlotValueType.Vector1:
                    return SlotValueType.Vector1;
                case ConcreteSlotValueType.Boolean:
                    return SlotValueType.Boolean;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static PropertyType ToPropertyType(this ConcreteSlotValueType concreteValueType)
        {
            switch (concreteValueType)
            {
                case ConcreteSlotValueType.SamplerState:
                    return PropertyType.SamplerState;
                case ConcreteSlotValueType.Matrix4:
                    return PropertyType.Matrix4;
                case ConcreteSlotValueType.Matrix3:
                    return PropertyType.Matrix3;
                case ConcreteSlotValueType.Matrix2:
                    return PropertyType.Matrix2;
                case ConcreteSlotValueType.Texture2D:
                    return PropertyType.Texture2D;
                case ConcreteSlotValueType.Texture2DArray:
                    return PropertyType.Texture2DArray;
                case ConcreteSlotValueType.Texture3D:
                    return PropertyType.Texture3D;
                case ConcreteSlotValueType.Cubemap:
                    return PropertyType.Cubemap;
                case ConcreteSlotValueType.Gradient:
                    return PropertyType.Gradient;
                case ConcreteSlotValueType.Vector4:
                    return PropertyType.Vector4;
                case ConcreteSlotValueType.Vector3:
                    return PropertyType.Vector3;
                case ConcreteSlotValueType.Vector2:
                    return PropertyType.Vector2;
                case ConcreteSlotValueType.Vector1:
                    return PropertyType.Vector1;
                case ConcreteSlotValueType.Boolean:
                    return PropertyType.Boolean;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static string ToShaderString(this ConcreteSlotValueType type, ConcretePrecision concretePrecision)
        {
            string precisionString = concretePrecision.ToShaderString();
            return type.ToShaderString(precisionString);
        }

        public static string ToShaderString(this ConcreteSlotValueType type, string precisionToken = PrecisionUtil.Token)
        {
            switch (type)
            {
                case ConcreteSlotValueType.SamplerState:
                    return "SamplerState";
                case ConcreteSlotValueType.Matrix4:
                    return precisionToken + "4x4";
                case ConcreteSlotValueType.Matrix3:
                    return precisionToken + "3x3";
                case ConcreteSlotValueType.Matrix2:
                    return precisionToken + "2x2";
                case ConcreteSlotValueType.Texture2D:
                    return "Texture2D";
                case ConcreteSlotValueType.Texture2DArray:
                    return "Texture2DArray";
                case ConcreteSlotValueType.Texture3D:
                    return "Texture3D";
                case ConcreteSlotValueType.Cubemap:
                    return "TextureCube";
                case ConcreteSlotValueType.Gradient:
                    return "Gradient";
                case ConcreteSlotValueType.Vector4:
                    return precisionToken + "4";
                case ConcreteSlotValueType.Vector3:
                    return precisionToken + "3";
                case ConcreteSlotValueType.Vector2:
                    return precisionToken + "2";
                case ConcreteSlotValueType.Vector1:
                    return precisionToken;
                case ConcreteSlotValueType.Boolean:
                    return precisionToken;
                default:
                    return "Error";
            }
        }

        public static string ToClassName(this ConcreteSlotValueType type)
        {
            return k_ConcreteSlotValueTypeClassNames[(int)type];
        }

        static readonly string[] k_ConcreteSlotValueTypeClassNames =
        {
            null,
            "typeMatrix",
            "typeMatrix",
            "typeMatrix",
            "typeTexture2D",
            "typeTexture2DArray",
            "typeTexture3D",
            "typeCubemap",
            "typeGradient",
            "typeFloat4",
            "typeFloat3",
            "typeFloat2",
            "typeFloat1",
            "typeBoolean"
        };
    }
}
