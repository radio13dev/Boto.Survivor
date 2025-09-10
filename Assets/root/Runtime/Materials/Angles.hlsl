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
#endif // ANGLES_INCLUDED
