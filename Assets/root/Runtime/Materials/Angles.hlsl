#ifndef ANGLES_INCLUDED
#define ANGLES_INCLUDED

void IsWithinRangeWrap_float(float angle, float direction, float range, float wrap, out float isWithin){
    angle = (angle + wrap)%wrap;
    direction = (direction + wrap)%wrap;
    float diff = (angle - direction + wrap) % wrap;
    if (diff > wrap - range || diff < range)
        isWithin = 1;
    else
        isWithin = 0;
}

float Repeat(float t, float length)
{
    return clamp(t-floor(t/length)*length,0,length);
}

float LerpRepeat(float a, float b, float t, float length)
{
    float num = Repeat(b - a, length*2);
    if (num > length)
        num -= length*2;
    return a + num * clamp(t,0,1);
}

float DeltaRepeat(float a, float b, float length)
{
    float num = Repeat(b - a, length*2);
    if (num > length)
        num -= length*2;
    return num;
}

float ease_cubic_out(float x)
{
    return 1 - pow(1-x,5);
}

void GroupFloat2_float(float2 uv, float2 offset, float2 grouping, float2 smoothing, out float2 uvOut)
{
    uv += offset;
    uv.x = LerpRepeat(uv.x, 0,
        grouping.x * clamp(ease_cubic_out(smoothing.x * abs(DeltaRepeat(0.5, uv.x, 0.5))), 0, 1), 0.5);
    uv.y = LerpRepeat(uv.y, 0,
        grouping.y * clamp(ease_cubic_out(smoothing.y * abs(DeltaRepeat(0.5, uv.y, 0.5))), 0, 1), 0.5);

    uvOut = uv;
}


half Repeat(half t, half length)
{
    return clamp(t-floor(t/length)*length,0,length);
}

half LerpRepeat(half a, half b, half t, half length)
{
    float num = Repeat(b - a, length*2);
    if (num > length)
        num -= length*2;
    return a + num * clamp(t,0,1);
}

half DeltaRepeat(half a, half b, half length)
{
    float num = Repeat(b - a, length*2);
    if (num > length)
        num -= length*2;
    return num;
}

half ease_cubic_out(half x)
{
    return 1 - pow(1-x,5);
}
void GroupFloat2_half(half2 uv, half2 offset, half2 grouping, half2 smoothing, out half2 uvOut)
{
    uv += offset;
    uv.x = LerpRepeat(uv.x, 0,
        grouping.x * clamp(ease_cubic_out(smoothing.x * abs(DeltaRepeat(0.5, uv.x, 0.5))), 0, 1), 0.5);
    uv.y = LerpRepeat(uv.y, 0,
        grouping.y * clamp(ease_cubic_out(smoothing.y * abs(DeltaRepeat(0.5, uv.y, 0.5))), 0, 1), 0.5);
    uvOut = uv;
}
#endif // ANGLES_INCLUDED
