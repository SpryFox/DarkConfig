using UnityEngine;
using System;
using System.Linq;

namespace SpryFox.Common {
    public class Assert {
        private const string s_assertGuard = "ASSERTS_ENABLED";

        [System.Diagnostics.Conditional(s_assertGuard)]
        public static void True(bool test, params object[] messages) {
            if (test == false) {
                // concatenate the messages only after the test fails
                string message = string.Join(" ", messages.Select(x => x.ToString()).ToArray());

                if (Application.isEditor && Application.isPlaying == false) {
                    // normal asserts don't work in the editor
                    throw new UnityException(message);
                } else {
                    Debug.LogError(message);
                    Debug.Break();
                }
            }
        }

        [System.Diagnostics.Conditional(s_assertGuard)]
        public static void ValueInRange<T>(T value, T minInclusive, T maxExclusive,
            params object[] messages)
            where T : IComparable<T> {
            True(minInclusive.CompareTo(value) <= 0 && maxExclusive.CompareTo(value) > 0,
                "value ", value, " not in range [", minInclusive, ", ", maxExclusive, ")",
                messages);
        }


        [System.Diagnostics.Conditional(s_assertGuard)]
        public static void IsNotNull(System.Object obj, params object[] msgs) {
            Assert.True(obj != null, msgs);
        }

        [System.Diagnostics.Conditional(s_assertGuard)]
        public static void IsNull(System.Object obj, params object[] msgs) {
            Assert.True(obj == null, msgs);
        }


        // tests for qnans
        [System.Diagnostics.Conditional(s_assertGuard)]
        public static void IsValid(float f, params object[] msgs) {
#pragma warning disable 1718
            Assert.True(f == f, msgs);
#pragma warning restore 1718
        }

        // tests for qnans
        [System.Diagnostics.Conditional(s_assertGuard)]
        public static void IsValid(Vector3 v, params object[] msgs) {
            Assert.IsValid(v.x, msgs);
            Assert.IsValid(v.y, msgs);
            Assert.IsValid(v.z, msgs);
        }

        // tests for qnans
        [System.Diagnostics.Conditional(s_assertGuard)]
        public static void IsValid(Quaternion q, params object[] msgs) {
            Assert.IsValid(q.x, msgs);
            Assert.IsValid(q.y, msgs);
            Assert.IsValid(q.z, msgs);
            Assert.IsValid(q.w, msgs);
        }

        // tests for qnans
        [System.Diagnostics.Conditional(s_assertGuard)]
        public static void IsValid(Transform t, params object[] msgs) {
            Assert.IsValid(t.position, msgs);
            Assert.IsValid(t.rotation, msgs);
        }

        [System.Diagnostics.Conditional(s_assertGuard)]
        public static void Fail(params object[] msgs) {
            True(false, msgs);
        }
    }
}