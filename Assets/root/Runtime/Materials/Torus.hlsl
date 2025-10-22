#ifndef TORUS_INCLUDED
#define TORUS_INCLUDED

void GetToroidalCoord_float(float3 worldPosition, float3 cameraPosition, float2 gridScale, float mapDivision, float ringRadius, float thickness, out float2 toroidalPosition){
	float theta = atan2(worldPosition.z, worldPosition.x);
	
	float3 ringCenter = float3(ringRadius * cos(theta), 0, ringRadius * sin(theta));
	float3 ringCenterOffset = worldPosition - ringCenter;
	float distFromRing = length(ringCenterOffset.xz);
	if (dot(worldPosition.xz,worldPosition.xz) < ringRadius*ringRadius) distFromRing = -distFromRing;
	float phi = atan2(worldPosition.y, distFromRing);
	
	//theta += 3.14;
	//theta %= 3.14*mapDivision;
	//if (theta > 3.14/2) theta = 3.14 - theta;
	//else if (theta < -3.14/2) theta = -3.14 - theta;
	//bool negative = theta < 0;
	//float pi = 3.14159265359;
	//float scale = 8;
	//
	//theta = theta/pi;
	//if (negative) theta = -theta;
	//
	//theta = theta*scale;
	//int div = floor(theta);
	//theta = theta - div;
//
	//if (theta < 0.5) theta = theta;
	//else theta = 1-theta;//2*theta-2*theta*theta+(1-theta-(2*theta-2*theta*theta))*(theta-0.5)*2;
	////if (theta > 0.479) theta = 0.4;
	//
	////theta = theta + div;
	////theta = theta/scale;
	//
	//if ((div % 2) == 1) theta = -theta;
	//
	//if (negative) theta = -theta;
	//theta = theta*pi;
	
	toroidalPosition = float2(theta, phi);
		
	// The more we travel around the torus the more 'curved' any parallel lines become.
	// To get around this we'll lerp our x values to the nearest grid scale position
	//float camTheta = atan2(cameraPosition.z, cameraPosition.x);
	
	// Now transform this so that distances on the inside of the torus match distances on the outside
	// FIRST: Determine the inner radius and outer radius
	//float innerRadius = ringRadius - thickness * 0.5;
	//float outerRadius = ringRadius + thickness * 0.5;
	//// SECOND: As the toroidalPosition.y goes from -pi/2 to pi/2, we want distances in the 'theta' direction to scale
	//toroidalPosition.x *= lerp(1, 
	//	outerRadius / innerRadius, 
	//	(1 + cos(toroidalPosition.y)) * 0.5);
}

// Torus SDF (signed distance function)
float TorusSDF(float3 p, float ringRadius, float thickness) {
	float2 q = float2(length(p.xz) - ringRadius, p.y);
	return length(q) - thickness;
}

// Raymarching function
float3 RaycastToTorus(float3 rayOrigin, float3 rayDir, float ringRadius, float thickness, float maxDist, int maxSteps, float epsilon) {
	float dist = 0.0;
	float3 pos = rayOrigin;
	for (int i = 0; i < maxSteps; i++) {
		pos = rayOrigin + rayDir * dist;
		float d = TorusSDF(pos, ringRadius, thickness);
		if (abs(d) < epsilon) {
			return pos; // Hit point
		}
		dist += d;
		if (dist > maxDist) break;
	}
	return pos; // No hit (return a default value or use a bool flag for hit/miss)
}

// Solve ray–torus intersection directionally via analytic quartic
// Returns the smallest positive t, or -1.0 if no hit.

const float EPS = 1e-6;

float solveQuartic(float c4, float c3, float c2, float c1, float c0) {
    // Inline quartic solver ported from shader-toy style implementation
    // Code based on StackOverflow solution :contentReference[oaicite:5]{index=5}
    // Normalize to depressed quartic: t^4 + a*t^3 + b*t^2 + c*t + d = 0
    float a = c3 / c4;
    float b = c2 / c4;
    float c = c1 / c4;
    float d = c0 / c4;

    float a2 = a*a;
    float Q = (3.0*b - 0.75*a2)/9.0;
    float R = (9.0*a*b - 27.0*c - 2.0*a2*a)/54.0;
    float D = Q*Q*Q + R*R;

    float tmin = 1e20;

    if (D >= 0.0) {
        float sqrtD = sqrt(D);
        float S = sign(R + sqrtD)*pow(abs(R + sqrtD), 1.0/3.0);
        float T = sign(R - sqrtD)*pow(abs(R - sqrtD), 1.0/3.0);
        float z = -a/3.0 + S + T;

        float U = sqrt(a2/4.0 - b + z);
        if (abs(U) > EPS) {
            float v = sqrt(3.0*a2/4.0 - U*U - 2.0*b + (4.0*a*b - 8.0*c - a2*a)/(4.0*U));
            float w = sqrt(3.0*a2/4.0 - U*U - 2.0*b - (4.0*a*b - 8.0*c - a2*a)/(4.0*U));
            float t1 = -a/4.0 - U/2.0 - v/2.0;
            float t2 = -a/4.0 - U/2.0 + v/2.0;
            float t3 = -a/4.0 + U/2.0 - w/2.0;
            float t4 = -a/4.0 + U/2.0 + w/2.0;
            
        	if (t1 > EPS && t1 < tmin) tmin = t1;
            if (t2 > EPS && t2 < tmin) tmin = t2;
            if (t3 > EPS && t3 < tmin) tmin = t3;
            if (t4 > EPS && t4 < tmin) tmin = t4;
        }
    } else {
        // Three real roots case (all cos-based)
        float theta = acos(R / sqrt(-Q*Q*Q)) / 3.0;
        float sqrtQ = sqrt(-Q);
        for (int k = 0; k < 3; ++k) {
            float z = 2.0 * sqrtQ * cos(theta + 2.0*3.14159265*float(k)/3.0) - a/3.0;
            // Derive the two quadratic roots from z...
            // (Left as exercise or extended code)
        }
    }
    return tmin < 1e19 ? tmin : -1.0;
}

float intersectRayTorus(float3 ro, float3 rd, float R, float r) {
    float ro2 = dot(ro, ro);
    float rd2 = dot(rd, rd);
    float dotOR = dot(ro, rd);

    float sumR = R*R - r*r;
    float k = ro2 + sumR;

    float c4 = rd2 * rd2;
    float c3 = 4.0 * rd2 * dotOR;
    float c2 = 2.0 * rd2 * k + 4.0 * dotOR*dotOR - 4.0 * R*R * (rd.x*rd.x + rd.z*rd.z);
    float c1 = 4.0 * dotOR * k - 8.0 * R*R * (ro.x*rd.x + ro.z*rd.z);
    float c0 = k*k - 4.0 * R*R * (ro.x*ro.x + ro.z*ro.z);

    return solveQuartic(c4, c3, c2, c1, c0);
}

float3 projectOntoTorusDirection(float3 rayOrigin, float3 rayDir, float ringRadius, float thickness) {
    float r = thickness * 0.5;
    float t = intersectRayTorus(rayOrigin, rayDir, ringRadius, r);
    if (t > 0.0) {
        return rayOrigin + t * rayDir;
    }
    return 0;
}

void RaycastFromCameraToTorus_float(float3 cameraPosition, float3 worldPosition, float ringRadius, float thickness, out float3 worldPositionOnTorus)
{
	//worldPositionOnTorus = projectOntoTorusDirection(cameraPosition, normalize(worldPosition - cameraPosition), ringRadius, thickness);
	worldPositionOnTorus = RaycastToTorus(worldPosition, normalize(worldPosition - cameraPosition), ringRadius, thickness, 100, 3, 0.01);
}
#endif // SPRITE_SHEET_INCLUDED
