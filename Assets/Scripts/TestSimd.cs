// using TMPro;
// using Unity.Burst;
// using Unity.Burst.Intrinsics;
// using Unity.Mathematics;
// using UnityEngine;
//
// public class TestSimd : MonoBehaviour
// {
//     [SerializeField] private TextMeshProUGUI text;
//     
//     private void OnEnable()
//     {
//         Debug.Log(Arm.Neon.IsNeonSupported);
//         var a = new v128(1.0f, 2.0f, 3.0f, 4.0f);
//         var b = new v128(5.0f, 6.0f, 7.0f, 8.0f);
//         var c = Arm.Neon.vmulq_f32(a, b);
//         Debug.Log($"c = {c.Float0}, {c.Float1}, {c.Float2}, {c.Float3}");
//     }
// }
//
// [BurstCompile]
// public static class TestAdd
// {
//     [BurstCompile]
//     public static void SseAdd(ref v128 a, ref v128 b, out v128 c)
//     {
//         c = X86.Sse.add_ps(a, b);
//     }
//
//     [BurstCompile]
//     public static void MathAdd(ref float4 a, ref float4 b, out float4 c)
//     {
//         c = a + b;
//     }
// }
