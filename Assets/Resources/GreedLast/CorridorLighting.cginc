#ifndef GREED_CORRIDOR_LIGHTING
#define GREED_CORRIDOR_LIGHTING

float3 GreedTorchLight(float3 course, float3 normal, float3 lamp)
{
    float3 delta = lamp - course;
    float squaredDistance = dot(delta, delta);
    float facing = saturate(dot(normal, delta) * rsqrt(max(0.01, squaredDistance)));
    float falloff = 1.0 / (1.0 + squaredDistance * 0.64);
    return float3(1.02, 0.66, 0.34) * (0.16 + facing * 0.84) * falloff;
}

float3 GreedCorridorLight(float3 course, float3 normal)
{
    float behind = floor((course.z - 1.0) * 0.25) * 4.0 + 1.0;
    float ahead = behind + 4.0;
    float between = smoothstep(0.0, 1.0, (course.z - behind) * 0.25);
    float3 leftBehind = GreedTorchLight(course, normal, float3(-3.00, 2.96, behind));
    float3 rightBehind = GreedTorchLight(course, normal, float3(3.00, 2.96, behind));
    float3 leftAhead = GreedTorchLight(course, normal, float3(-3.00, 2.96, ahead));
    float3 rightAhead = GreedTorchLight(course, normal, float3(3.00, 2.96, ahead));
    float3 torches = lerp(leftBehind + rightBehind, leftAhead + rightAhead, between);
    float upperFill = saturate(normal.y * 0.58 + 0.42);
    return float3(0.54, 0.57, 0.60) + upperFill * float3(0.25, 0.22, 0.18) + torches;
}

float3 GreedCorridorAir(float3 color, float distance)
{
    float haze = smoothstep(7.0, 52.0, distance);
    float3 air = lerp(float3(0.18, 0.15, 0.12), float3(0.075, 0.086, 0.090), haze);
    return lerp(color, air, haze);
}

#endif
