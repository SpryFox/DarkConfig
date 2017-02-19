using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace SpryFox.Common
{
    public static class Vector3Extensions {
        public static Vector2 XY(this Vector3 self) {
            return new Vector2(self.x, self.y);
        }

        public static Vector3 ProjectOntoXZ(this Vector3 self) {
            return new Vector3(self.x, 0, self.z);
        }

        public static Vector3 ProjectOntoXY(this Vector3 self) {
            return new Vector3(self.x, self.y);
        }

        public static Vector3 InvertScreenY(this Vector3 self) {
            return new Vector3(self.x, Screen.height - self.y, self.z);
        }

        public static Vector3 Left2D(this Vector3 self) {
            return new Vector3(-self.y, self.x, 0);
        }

        public static Vector3 Right2D(this Vector3 self) {
            return new Vector3(self.y, -self.x, 0);
        }

        // because we can't do Vector3? as it's a struct
        public struct VectorResult {

            public static VectorResult CreateValid(Vector3 result) {
                return new VectorResult {
                    Result = result,
                    IsValid = true,
                };
            }
        
            public static readonly VectorResult INVALID 
            = new VectorResult { IsValid = false, };

            public Vector3 Result;
            public bool IsValid;
        }

        public static VectorResult IntersectionWithPlane(
            this Ray ray, Vector3 planeOrigin, Vector3 planeNormal) {
        
            float rayPlaneParallelness = Vector3.Dot(planeNormal, ray.direction);
            float rayOriginPlaneOffset = Vector3.Dot((planeOrigin - ray.origin), planeNormal);
        
            if (Mathf.Approximately(rayPlaneParallelness, 0f)) {
                // ray parallel to plane. can only intersect if starts inside plane!

                if (Mathf.Approximately(rayOriginPlaneOffset, 0f)) {
                    return VectorResult.CreateValid(ray.origin);
                } else {
                    return VectorResult.INVALID;
                }

            } else {
                float rayT = rayOriginPlaneOffset / rayPlaneParallelness;
                var intersectionPoint = ray.origin + ray.direction * rayT;
                return VectorResult.CreateValid(intersectionPoint);
            }
        }

        // Tells you which side of the directed line the point inhabits.
        // > 0: left
        // == 0: on
        // < 0: right
        public static float LineSide(this Vector3 self, Vector3 l0, Vector3 l1) {
            return (l1.x - l0.x) * (self.y - l0.y) - (self.x - l0.x) * (l1.y - l0.y);
        }

        public static Vector3 ClosestPointOnLine(this Vector3 self, Vector3 l0, Vector3 l1) {
            Vector3 vp = self - l0;
            Vector3 vl = l1 - l0;
            float t = Vector3.Dot(vp, vl) / vl.sqrMagnitude;
            return l0 + vl * t;
        }

        public static Vector3 ClosestPointOnSegment(this Vector3 self, Vector3 l0, Vector3 l1) {
            Vector3 vp = self - l0;
            Vector3 vl = l1 - l0;
            float t = Mathf.Clamp01(Vector3.Dot(vp, vl) / vl.sqrMagnitude);
            return l0 + vl * t;
        }

        public const float layerResolution = 64;
        public const float layerDelta = 1f / layerResolution;
        public static Vector3 SetLayer(this Vector3 self, int layer) {
            self.z = 0 + (layerDelta * layer);
            return self;
        }

        public static Vector3 IncrLayer(this Vector3 self, int incr) {
            self.z = self.z + (layerDelta * incr);
            return self;
        }

        public static int GetLayer(this Vector3 self) {
            return Mathf.RoundToInt(self.z * layerResolution);
        }

        public static Vector3 RotateBy2D(this Vector3 self, float angle) {
            Quaternion q = Quaternion.AngleAxis(angle, Vector3.forward);
            return q * self;
        }

        public static float Angle2D(this Vector3 self) {
            return Mathf.Atan2(self.y, self.x) * Mathf.Rad2Deg;
        }

        public static Vector3 LimitFractional(this Vector3 self, long fraction = MathPlus.defaultFraction) {
            return new Vector3(
                ((long)(self.x * fraction)) / (float)fraction,
                ((long)(self.y * fraction)) / (float)fraction,
                ((long)(self.z * fraction)) / (float)fraction);
        }

        public static Vector3 SnapToGrid(this Vector3 self, float gridSize) {
            return new Vector3(((long)(self.x / gridSize)) * gridSize,
                               ((long)(self.y / gridSize)) * gridSize,
                               ((long)(self.z / gridSize)) * gridSize);
        }

        public static Vector3 MulPointwise(this Vector3 self, Vector3 o) {
            return new Vector3(self.x * o.x, self.y * o.y, self.z * o.z);
        }

        public static Vector3 SetX(this Vector3 self, float x) {
            return new Vector3(x, self.y, self.z);
        }

        public static Vector3 SetY(this Vector3 self, float y) {
            return new Vector3(self.x, y, self.z);
        }

        public static Vector3 SetZ(this Vector3 self, float z) {
            return new Vector3(self.x, self.y, z);
        }

        public static string ToJSON(this Vector3 self) {
            return "[" + self.x + ", " + self.y + ", " + self.z + "]";
        }
    }

    public static class Vector2Extensions {
        public static Vector3 XYZ1(this Vector2 self) {
            return new Vector3(self.x, self.y, 1);
        }

        public static Vector2 InvertY(this Vector2 self) {
            return new Vector2(self.x, -self.y);
        }

        public static Vector2 InvertScreenY(this Vector2 self) {
            return new Vector2(self.x, Screen.height - self.y);
        }
	
        public static Vector2 LimitFractional2(this Vector2 self, long fraction = MathPlus.defaultFraction) {
            return new Vector2(
                ((long)(self.x * fraction)) / (float)fraction,
                ((long)(self.y * fraction)) / (float)fraction);
        }

        public static Vector3 XYZ(this Vector2 self) {
            return self;
        }

        public static Vector2 FromAngle(float angle) {
            // angle is in degrees
            return new Vector2(Mathf.Cos(Mathf.Deg2Rad * angle), Mathf.Sin(Mathf.Deg2Rad * angle));
        }

        public static Vector2 Left2D(this Vector2 self) {
            return new Vector2(-self.y, self.x);
        }

        public static Vector2 Right2D(this Vector2 self) {
            return new Vector2(self.y, -self.x);
        }

        // removes negative signs from components
        public static Vector2 Abs(this Vector2 self) {
            return new Vector2(Mathf.Abs(self.x), Mathf.Abs(self.y));
        }
    }

    public static class QuaternionExtensions {
        public static Quaternion LimitFractional(this Quaternion self, long fraction = MathPlus.defaultFraction) {
            return new Quaternion(
                ((long)(self.x * fraction)) / (float)fraction,
                ((long)(self.y * fraction)) / (float)fraction,
                ((long)(self.z * fraction)) / (float)fraction,
                ((long)(self.w * fraction)) / (float)fraction);
        }
    }

    public static class MathPlus {

        // if negative, makes more negative. if positive, makes more positive.
        public static float IncreaseMagnitude(this float val, float absoluteIncrease) {
            if (val < 0f) {
                return val - absoluteIncrease;
            }
            else {
                return val + absoluteIncrease;
            }
        }

        // projects value from one range to another range
        public static float MapValue(float value,
                                     float inMin, float inMax,
                                     float outMin, float outMax) {

            float t = Mathf.InverseLerp(inMin, inMax, value);
            float outValue = Mathf.Lerp(outMin, outMax, t);
            return outValue;        
        }

        public static float NormalizeAngle(float angle) {
            while (angle > 180) angle -= 360;
            while (angle < -180) angle += 360;
            return angle;
        }

        public static int Wrap(int i, int cap) {
            while (i < 0) i = cap + i;
            i = i % cap;
            return i;
        }

        public static int CircularAdd(int a, int b, int cap) {
            return Wrap(a + b, cap);
        }

        public static int CircularDifference(int a, int b, int cap) {
            Assert.True(a >= 0 && b >= 0 && a < cap && b < cap, "Attempting to circular subtract two indices that aren't within the cap", a, b, cap);
            if (a - b == -1 || a - b == 1 || a - b == 0) return a - b;
            if (a < b) {
                a += cap;
                return b - a;
            } else {
                b += cap;
                return a - b;
            }
        }

        public static int Clamp(int i, int min, int max) {
            if(i < min) return min;
            if(i > max) return max;
            return i;
        }

        public const long defaultFraction = 1024;
        public static float LimitFractional(float x, long fraction = defaultFraction) {
            return ((long)(x * fraction)) / (float)fraction;
        }

        public static int NextPowerOf2(int val) {
            // http://acius2.blogspot.com/2007/11/calculating-next-power-of-2.html
            val--;
            val = (val >> 1) | val;
            val = (val >> 2) | val;
            val = (val >> 4) | val;
            val = (val >> 8) | val;
            val = (val >> 16) | val;
            val++;
            return val;
        }

        public static float MovingAvg(float source, float newValue, float increment) {
            if (source == 0) source = newValue; // initial value
            return Mathf.Lerp(source, newValue, increment);
        }

        public const double DEpsilon = 1e-45;
        public static bool Approximately(double a, double b) {
            if (a < b) {
                return (b - a) < DEpsilon;
            } else {
                return (a - b) < DEpsilon;
            }
        }

        public static void MeanAndStdev(IEnumerable<double> samples, out double mean, out double stdev) {
            // Welford
            mean = 0;
            double svar = 0;
            int n = 1;
            foreach (double d in samples) {
                double old_mean = mean;
                mean += (d - old_mean) / n;
                svar += (d - old_mean) * (d - mean);
                n++;
            }
            if (n > 1) {
                stdev = System.Math.Sqrt(svar / (n - 1));
            } else {
                stdev = 0;
            }
        }

        // note that the ulp/nextfloat/prevfloat functions only work well on "normal"
        // floats, which is to say nonzero noninfinite nonspecial
        public static float Ulp(float value) {
            return NextFloat(value) - value;
        }

        public static float NextFloat(float value) {
            long bits = System.BitConverter.ToInt32(System.BitConverter.GetBytes(value), 0);
            return System.BitConverter.ToSingle(System.BitConverter.GetBytes(bits + 1), 0);
        }

        public static float PrevFloat(float value) {
            long bits = System.BitConverter.ToInt32(System.BitConverter.GetBytes(value), 0);
            return System.BitConverter.ToSingle(System.BitConverter.GetBytes(bits - 1), 0);
        }
    }

    public struct LineSegment {
        public Vector3 S;
        public Vector3 E;
        public LineSegment(Vector3 s, Vector3 e) {
            S = s;
            E = e;
        }
    }


    public static class RectExtensions {
        public static bool Intersects(this Rect a, Rect b) {
            return !((a.xMin > b.xMax) || 
                     (a.xMax < b.xMin) ||
                     (a.yMin > b.yMax) ||
                     (a.yMax < b.yMin));
        }

        public static Rect Join(this Rect a, Rect b) {
            float x = a.xMin < b.xMin ? a.xMin : b.xMin;
            float y = a.yMin < b.yMin ? a.yMin : b.yMin;
            var r = new Rect(
                x,
                y,
                (a.xMax > b.xMax ? a.xMax : b.xMax) - x,
                (a.yMax > b.yMax ? a.yMax : b.yMax) - y);

            // This deals with a strange case: due to the way that rects are 
            // stored as a position and extent and the rounding errors of 
            // floating-point math, it's possible for a joined rectangle to have
            // a Max value smaller than the corresponding Max value of the larger
            // of the two parent Rects.
            // The fix is to detect this case and to add the smallest possible
            // value to the offending extent that will push it over the line and
            // be larger than either one of its parents' Maxes.
            // It's two ulps because occasionally the rounding involved in
            // computing the Max obliterates one ulp
            if(r.xMax < a.xMax || r.xMax < b.xMax) {
                r.width += Mathf.Abs(MathPlus.Ulp(r.width)) * 2;
            }
            if(r.yMax < a.yMax || r.yMax < b.yMax) {
                r.height += Mathf.Abs(MathPlus.Ulp(r.height)) * 2;
            }
            return r;
        }

        public static bool ContainsStrict(this Rect a, Rect b) {
            return b.xMax < a.xMax &&
                b.xMin > a.xMin &&
                b.yMin > a.yMin &&
                b.yMax < a.yMax;
        }

        public static bool Contains(this Rect a, Rect b) {
            return b.xMin >= a.xMin &&
                b.xMax <= a.xMax &&
                b.yMin >= a.yMin &&
                b.yMax <= a.yMax;
        }

        // if pos is outside bounds, clamps pos to the edge
        public static Vector2 ClipPosition(this Rect bounds, Vector2 p)
        {
            float clampedX = Mathf.Clamp(p.x, bounds.xMin, bounds.xMax);
            float clampedY = Mathf.Clamp(p.y, bounds.yMin, bounds.yMax);
            var clampedPos = new Vector2(clampedX, clampedY);
            return clampedPos;
        }

        // returns how long ray can be and still remain in box.
        // if ray starts outside box then returns rayLength.
        // if ray starts inside box but stops before box edge, returns rayLength
        public static float ClipRay(this Rect box,
                                    Vector2 rayOrigin, Vector2 rayDir, float rayLength)
        {
            if (box.Contains(rayOrigin) == false)
            {
                return rayLength;
            }

            var rayEnd = rayOrigin + rayDir * rayLength;
            float clippedRayLength = rayLength;

            if (rayEnd.x > box.xMax)
            {
                float horizontalDistanceToEdge = box.xMax - rayOrigin.x;
                clippedRayLength = horizontalDistanceToEdge / rayDir.x;
            }
            else if (rayEnd.x < box.xMin)
            {
                float horizontalDistanceToEdge = box.xMin - rayOrigin.x;
                clippedRayLength = horizontalDistanceToEdge / rayDir.x;
            }

            if (rayEnd.y > box.yMax)
            {
                float verticalDistanceToEdge = box.yMax - rayOrigin.y;
                float clippedLengthToTopEdge = verticalDistanceToEdge / rayDir.y;
                clippedRayLength = Mathf.Min(clippedRayLength, clippedLengthToTopEdge);
            }
            else if (rayEnd.y < box.yMin)
            {
                float verticalDistanceToEdge = box.yMin - rayOrigin.y;
                float clippedLengthToBottomEdge = verticalDistanceToEdge / rayDir.y;
                clippedRayLength = Mathf.Min(clippedRayLength, clippedLengthToBottomEdge);
            }

            return clippedRayLength;
        }

    }
}