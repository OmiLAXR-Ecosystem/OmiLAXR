using System;
using System.Collections.Generic;
using OmiLAXR.Extensions;
using OmiLAXR.Types;

namespace OmiLAXR.Components
{
    public abstract class FaceData
    {
        protected readonly Dictionary<FaceActionUnit, float> AuValues;
        protected readonly Dictionary<FaceActionUnit, float> AuConfidences;

        public int Count => Weights.Length; // assuming same length

        // Fast, allocation-free reads
        public float this[int i] => Weights[i];
        public float GetConfidence(int i) => Confidences[i];

        // If you want a safe enumerable:
        public float[] Weights { get; }

        public float[] Confidences { get; }

        public FaceData(float[] weights, float[] confidences)
        {
            Weights = weights;
            Confidences    = confidences;
            AuValues = new Dictionary<FaceActionUnit, float>();
            AuConfidences = new Dictionary<FaceActionUnit, float>();
        }
        
        public float GetAU(FaceActionUnit au)
        {
            return AuValues.TryGetValue(au, out var value) ? value : 0f;
        }

        public float GetConfidence(FaceActionUnit au)
        {
            return AuConfidences.TryGetValue(au, out var value) ? value : 1f;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is FaceData fd))
                return false;
            return ReferenceEquals(this, fd) ||
                   (fd.Weights.AreValuesEqual(Weights) && fd.Confidences.AreValuesEqual(Confidences));
        }
        
        public override int GetHashCode()
        {
            // Hash aus den Array-Inhalten deterministisch und ohne Allokationen bilden
            var hc = new HashCode();

            // Einbeziehen der Längen zur Unterscheidung unterschiedlicher Größen
            hc.Add(Weights?.Length ?? 0);
            for (int i = 0; i < (Weights?.Length ?? 0); i++)
                hc.Add(Weights[i]);

            hc.Add(Confidences?.Length ?? 0);
            for (int i = 0; i < (Confidences?.Length ?? 0); i++)
                hc.Add(Confidences[i]);

            return hc.ToHashCode();
        }

    }
}