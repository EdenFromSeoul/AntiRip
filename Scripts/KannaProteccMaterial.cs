﻿#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Kanna.Protecc
{
    public class KannaDynamicShaderData
    {
        public class KannaReplaceText
        {
            public string[] TextToFind;
            public string TextToReplaceWith;
            public bool ApplyToIncludes;
            public string[] ExcludeIncludes = {};
        }

        public string ShaderName_StartsWith;

        public KannaReplaceText UV;
        public KannaReplaceText Vert;
        public KannaReplaceText VertexSetup;
    }

    public static class KannaProteccMaterial
    {
        public static string GenerateDecodeShader(KannaProteccData data, bool[] keys)
        {
            // Set this because someone from russia was getting ,'s in their decimals instead of .'s
            Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");

            ModelShaderDecodeFirst = "";

            for (var i = 0; i < keys.Length; i++) // Add bitKey fields
            {
                ModelShaderDecodeFirst += $"float _BitKey{i};\r\n";
            }

            ModelShaderDecodeFirst += "\r\nfloat4 modelDecode(float4 vertex, float3 normal, float2 uv0, float2 uv1)\r\n{\r\n    // KannaProtecc Randomly Generated Begin\r\n"; // Finish Off First Part

            // Make ModelShaderDecodeSecond dynamic too, and make the comKey{i} based on a ideal count. Context: this is for ShaderLab.

            ModelShaderDecodeSecond = "    // KannaProtecc Randomly Generated End\r\n\r\n";

            var isY = false;

            for (var i = 0; i < (data.ComKey.Length) / 2; i++) // theoretically should be exactly 50% of the length
            {
                ModelShaderDecodeSecond += $"    vertex.xyz -= normal * (uv0.{(!isY ? "x" : "y")} * comKey{i});\r\n";
                isY = !isY;
            }

            isY = !isY;

            ModelShaderDecodeSecond += "\r\n";

            for (var i = (data.ComKey.Length) / 2; i < data.ComKey.Length; i++) // theoretically should be exactly after 50%
            {
                ModelShaderDecodeSecond += $"    vertex.xyz -= normal * (uv1.{(!isY ? "x" : "y")} * comKey{i});\r\n";
                isY = !isY;
            }

            ModelShaderDecodeSecond += "\r\n    return vertex;\r\n}\r\n";

            var decodeKeys = new float[data.DividedCount];
            
            for (var i = 0; i < data.DividedCount; ++i)
            {
                decodeKeys[i] = (Convert.ToSingle(keys[i*KannaProteccData.CountDivisor]) + data.RandomKeyMultiplier[i*KannaProteccData.CountDivisor]) *
                                (Convert.ToSingle(keys[i*KannaProteccData.CountDivisor+1]) + data.RandomKeyMultiplier[i*KannaProteccData.CountDivisor+1]) *
                                (Convert.ToSingle(keys[i*KannaProteccData.CountDivisor+2]) + data.RandomKeyMultiplier[i*KannaProteccData.CountDivisor+2]) *
                                (Convert.ToSingle(keys[i*KannaProteccData.CountDivisor+3]) + data.RandomKeyMultiplier[i*KannaProteccData.CountDivisor+3]);
            }

            var sb0 = new StringBuilder();
            var sb1 = new StringBuilder();
            for (var i = 0; i < data.DividedCount; ++i)
            {
                var firstAdd = data.KeySign0[i] > 0
                    ? decodeKeys[data.RandomKeyIndex[i*KannaProteccData.CountDivisor]] - decodeKeys[data.RandomKeyIndex[i*KannaProteccData.CountDivisor+1]]
                    : decodeKeys[data.RandomKeyIndex[i*KannaProteccData.CountDivisor]] + decodeKeys[data.RandomKeyIndex[i*KannaProteccData.CountDivisor+1]];
                var firstSign = data.Sign0[i] > 0 ? Mathf.Sin(firstAdd) : Mathf.Cos(firstAdd);

                var secondAdd = data.KeySign1[i] > 0
                    ? decodeKeys[data.RandomKeyIndex[i*KannaProteccData.CountDivisor+2]] - decodeKeys[data.RandomKeyIndex[i*KannaProteccData.CountDivisor+3]]
                    : decodeKeys[data.RandomKeyIndex[i*KannaProteccData.CountDivisor+2]] + decodeKeys[data.RandomKeyIndex[i*KannaProteccData.CountDivisor+3]];
                var secondSign = data.Sign1[i] > 0 ? Mathf.Sin(secondAdd) : Mathf.Cos(secondAdd);

                data.ComKey[i] = firstSign * data.RandomDivideMultiplier[i] * secondSign;

                var firstAddStr = data.KeySign0[i] > 0 ? "-" : "+";
                var firstSignStr = data.Sign0[i] > 0 ? "sin" : "cos";
                var secondAddStr = data.KeySign1[i] > 0 ? "-" : "+";
                var secondSignStr = data.Sign1[i] > 0 ? "sin" : "cos";

                sb0.AppendLine($"    float decodeKey{i} = (_BitKey{i*KannaProteccData.CountDivisor} + {data.RandomKeyMultiplier[i*KannaProteccData.CountDivisor]}) * (_BitKey{i*KannaProteccData.CountDivisor+1} + {data.RandomKeyMultiplier[i*KannaProteccData.CountDivisor+1]}) * (_BitKey{i*KannaProteccData.CountDivisor+2} + {data.RandomKeyMultiplier[i*KannaProteccData.CountDivisor+2]}) * (_BitKey{i*KannaProteccData.CountDivisor+3} + {data.RandomKeyMultiplier[i*KannaProteccData.CountDivisor+3]});");

                sb1.AppendLine($"    float comKey{i} = {firstSignStr}(decodeKey{data.RandomKeyIndex[i*KannaProteccData.CountDivisor]} {firstAddStr} decodeKey{data.RandomKeyIndex[i*KannaProteccData.CountDivisor+1]}) * {data.RandomDivideMultiplier[i]} * {secondSignStr}(decodeKey{data.RandomKeyIndex[i*KannaProteccData.CountDivisor+2]} {secondAddStr} decodeKey{data.RandomKeyIndex[i*KannaProteccData.CountDivisor+3]});");
            }

            var decodeShader = $"{ModelDecodeIfndef}{ModelShaderDecodeFirst}{sb0}\r\n{sb1}{ModelShaderDecodeSecond}{ModelDecodeEndif}";
            return decodeShader;
        }

        public const string ModelDecodeIfndef = "#ifndef KANNAMODELDECODE\r\n#define KANNAMODELDECODE\r\n";
        
        public const string ModelDecodeEndif = "#endif\r\n";
        
        static string ModelShaderDecodeFirst = 
@"float _BitKey0;
float _BitKey1;
float _BitKey2;
float _BitKey3;

float _BitKey4;
float _BitKey5;
float _BitKey6;
float _BitKey7;

float _BitKey8;
float _BitKey9;
float _BitKey10;
float _BitKey11;

float _BitKey12;
float _BitKey13;
float _BitKey14;
float _BitKey15;

float _BitKey16;
float _BitKey17;
float _BitKey18;
float _BitKey19;

float _BitKey20;
float _BitKey21;
float _BitKey22;
float _BitKey23;

float _BitKey24;
float _BitKey25;
float _BitKey26;
float _BitKey27;

float _BitKey28;
float _BitKey29;
float _BitKey30;
float _BitKey31;

float4 modelDecode(float4 vertex, float3 normal, float2 uv0, float2 uv1)
{
    // KannaProtecc Randomly Generated Begin
";

        // 1/2 divided length to each uv, total comKey count is 
        static string ModelShaderDecodeSecond =
@" 
    // KannaProtecc Randomly Generated End

    vertex.xyz -= normal * (uv0.x * comKey0);
    vertex.xyz -= normal * (uv0.y * comKey1);
    vertex.xyz -= normal * (uv0.x * comKey2);
    vertex.xyz -= normal * (uv0.y * comKey3);

    vertex.xyz -= normal * (uv1.y * comKey4);
    vertex.xyz -= normal * (uv1.x * comKey5);
    vertex.xyz -= normal * (uv1.y * comKey6);
    vertex.xyz -= normal * (uv1.x * comKey7);

    return vertex;
}";
    }

    public class KannaProteccData
    {
        public const int CountDivisor = 4;

        public readonly int DividedCount;
        public readonly int[] Sign0;
        public readonly int[] KeySign0;
        public readonly int[] RandomKeyIndex;
        public readonly int[] Sign1;
        public readonly int[] KeySign1;
        public readonly float[] RandomDivideMultiplier;
        public readonly float[] RandomKeyMultiplier;
        public readonly float[] ComKey;

        void Shuffle<T>(IList<T> list)
        {
            using (var provider = new RNGCryptoServiceProvider())
            {
                var n = list.Count;
                while (n > 1)
                {
                    var box = new byte[1];
                    do provider.GetBytes(box);
                    while (!(box[0] < n * (Byte.MaxValue / n)));
                    var k = (box[0] % n);
                    n--;
                    var value = list[k];
                    list[k] = list[n];
                    list[n] = value;
                }
            }
        }
        
        public KannaProteccData(int count)
        {
            DividedCount = count / CountDivisor;
            Sign0 = new int[DividedCount];

            var  randomKeyIndexList = new List<int>();
            
            KeySign0 = new int[DividedCount];
            Sign1 = new int[DividedCount];
            KeySign1 = new int[DividedCount];
            RandomDivideMultiplier = new float[DividedCount];
            RandomKeyMultiplier = new float[count];
            ComKey = new float[DividedCount];

            for (var i = 0; i < DividedCount; ++i)
            {
                Sign0[i] = Random.Range(0, 2);
                KeySign0[i] = Random.Range(0, 2);
                Sign1[i] = Random.Range(0, 2);
                KeySign1[i] = Random.Range(0, 2);

                randomKeyIndexList.Add(i);
                randomKeyIndexList.Add(i);
                randomKeyIndexList.Add(i);
                randomKeyIndexList.Add(i);
                
                RandomDivideMultiplier[i] = Random.Range(0f, 2f);
            }
            
            Shuffle(randomKeyIndexList);
            RandomKeyIndex = randomKeyIndexList.ToArray();

            for (var i = 0; i < count; ++i)
            {
                RandomKeyMultiplier[i] = Random.Range(0f, 2f);
            }
        }
    }
}
#endif
