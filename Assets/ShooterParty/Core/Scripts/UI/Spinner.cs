using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterParty.UI
{
    // Turns slowly while something is loading.
    public class Spinner : MonoBehaviour
    {
        public float degreesPerSecond = -300f;
        void Update() => transform.Rotate(0f, 0f, degreesPerSecond * Time.unscaledDeltaTime);
    }
}
