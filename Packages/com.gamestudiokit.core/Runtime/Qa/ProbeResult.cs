// Game Studio Kit (MIT). One measured metric with its threshold and verdict: checks.schema.json#/$defs/probeResult.
// Engine-free so the node tests can serialise it under plain .NET.
using GameStudioKit.Common;

namespace GameStudioKit.Qa
{
    /// <summary>PASS, FAIL, or ROUTE (send to the reviewer; never an automatic failure).</summary>
    public enum ProbeVerdict
    {
        PASS,
        FAIL,
        ROUTE,
    }

    public sealed class ProbeResult : IKitJson
    {
        public string Probe;
        public string Body;
        public string Clip;
        public string Metric;
        public string Unit;

        /// <summary>The numeric value; ignored when <see cref="BoolValue"/> is set.</summary>
        public double Value;
        public bool? BoolValue;

        public string ThresholdOp;
        public double? ThresholdNumber;
        public bool? ThresholdBool;

        /// <summary>Body-class scale applied to the threshold (body height / reference height); null when not scaled.</summary>
        public double? K;
        public ProbeVerdict Verdict;

        public bool HasThreshold => ThresholdOp != null && (ThresholdNumber.HasValue || ThresholdBool.HasValue);

        public void WriteJson(KitJsonWriter w)
        {
            w.BeginObject();
            w.Prop("probe", Probe ?? "unknown");
            if (!string.IsNullOrEmpty(Body)) w.Prop("body", Body);
            if (!string.IsNullOrEmpty(Clip)) w.Prop("clip", Clip);
            w.Prop("metric", Metric ?? "unknown");
            w.Name("value");
            if (BoolValue.HasValue) w.Value(BoolValue.Value);
            else if (double.IsNaN(Value) || double.IsInfinity(Value)) w.Value(-1.0); // schema needs a number; verdict is FAIL
            else w.Value(Value);
            w.Prop("unit", string.IsNullOrEmpty(Unit) ? "unitless" : Unit);
            if (HasThreshold)
            {
                w.Name("threshold").BeginObject();
                w.Prop("op", ThresholdOp);
                w.Name("value");
                if (ThresholdBool.HasValue) w.Value(ThresholdBool.Value);
                else w.Value(ThresholdNumber.Value);
                w.EndObject();
            }
            if (K.HasValue && K.Value > 0) w.Prop("k", K.Value);
            w.Prop("verdict", Verdict.ToString());
            w.EndObject();
        }

        public override string ToString()
        {
            string v = BoolValue.HasValue ? (BoolValue.Value ? "true" : "false") : KitJsonWriter.FormatNumber(Value);
            string t = !HasThreshold ? "no threshold"
                : ThresholdOp + " " + (ThresholdBool.HasValue ? (ThresholdBool.Value ? "true" : "false") : KitJsonWriter.FormatNumber(ThresholdNumber.Value));
            return $"{Probe} {Metric}{(string.IsNullOrEmpty(Body) ? "" : " [" + Body + "]")}{(string.IsNullOrEmpty(Clip) ? "" : " (" + Clip + ")")} = {v} {Unit} ({t}) {Verdict}";
        }
    }
}
