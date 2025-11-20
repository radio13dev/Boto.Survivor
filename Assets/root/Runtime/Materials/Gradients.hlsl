#ifndef GRADIENTS_INCLUDED
#define GRADIENTS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/Functions.hlsl"

static const uint Lengths[4*2] = {
    3,2,
    3,2,
    4,2,
    7,2
};
static const float4 Gradients[8*4] = {
    // Common
    float4(0.251, 0.251, 0.251,0),
    float4(0.098, 0.098, 0.098,0.5),
    float4(0.251, 0.251, 0.251,1),
    float4(0,0,0,0),
    float4(0,0,0,0),
    float4(0,0,0,0),
    float4(0,0,0,0),
    float4(0,0,0,0),
    
    // Rare
    float4(0.318, 0.31, 1,0),
    float4(0.157, 0.949, 1,0.5),
    float4(0.318, 0.31, 1,1),
    float4(0,0,0,0),
    float4(0,0,0,0),
    float4(0,0,0,0),
    float4(0,0,0,0),
    float4(0,0,0,0),
    
    // Epic
    float4(1, 0.4, 0.118,0),
    float4(1, 0.671, 0.157,0.29),
    float4(1.34143198,0.26257208,0.208807826,0.68),
    float4(1, 0.4, 0.118,1),
    float4(0,0,0,0),
    float4(0,0,0,0),
    float4(0,0,0,0),
    float4(0,0,0,0),
    
    // Legendary
    float4(0.957, 0.514, 0.122,0),
    float4(0.694, 0.129, 0.141,0.166),
    float4(0.502, 0.106, 0.447,0.333),
    float4(0.149, 0.161, 0.671,0.5),
    float4(0.086, 0.592, 0.341,0.679),
    float4(0.282, 0.898, 0.098,0.844),
    float4(0.957, 0.514, 0.122,1),
    float4(0,0,0,0),
};  
static const float2 Alphas[8*4] = {
    // Common
    float2(1,0),
    float2(1,1),
    float2(1,0),
    float2(1,0),
    float2(1,0),
    float2(1,0),
    float2(1,0),
    float2(1,0),
    
    // Rare
    float2(1,0),
    float2(1,1),
    float2(1,0),
    float2(1,0),
    float2(1,0),
    float2(1,0),
    float2(1,0),
    float2(1,0),
    
    // Epic
    float2(1,0),
    float2(1,1),
    float2(1,0),
    float2(1,0),
    float2(1,0),
    float2(1,0),
    float2(1,0),
    float2(1,0),
    
    // Legendary
    float2(1,0),
    float2(1,1),
    float2(1,0),
    float2(1,0),
    float2(1,0),
    float2(1,0),
    float2(1,0),
    float2(1,0),
};

void SampleGradientIndex_float(float Gradient, float Time, out float4 Out){
    int gradientIndex = (int)Gradient*8;
    int gradientLength = Lengths[Gradient*2];
    float3 color = LinearToOklab(Gradients[gradientIndex].rgb);
    [unroll]
    for (int c = 1; c < 8; c++)
    {
        float colorPos = saturate((Time - Gradients[gradientIndex + c-1].w) / (Gradients[gradientIndex + c].w - Gradients[gradientIndex + c-1].w)) * step(c, gradientLength-1);
        color = lerp(color, LinearToOklab(Gradients[gradientIndex + c].rgb), colorPos);
    }
    color = OklabToLinear(color);
    #ifdef UNITY_COLORSPACE_GAMMA
    color = LinearToSRGB(color);
    #endif
    
    int alphaLength = Lengths[Gradient+1];
    float alpha = Alphas[gradientIndex].x;
    [unroll]
    for (int a = 1; a < 8; a++)
    {
        float alphaPos = saturate((Time - Alphas[gradientIndex + a-1].y) / (Alphas[gradientIndex + a].y - Alphas[gradientIndex + a-1].y)) * step(a, alphaLength-1);
        alpha = lerp(alpha, Alphas[gradientIndex + a].x, alphaPos);
    }
    Out = float4(color, alpha);
}
#endif // GRADIENTS_INCLUDED
