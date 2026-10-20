using System;
namespace NwafuConnect {
 public sealed class EvidenceAssessment {public bool Passed;public bool MissingEvidence;public double? ElapsedMs;}
 public static class EvidenceEvaluator {
  public static EvidenceAssessment Evaluate(DateTime? login,DateTime? network,DateTime process,DateTime? internet,DateTime boot){
   if(!login.HasValue||!network.HasValue||!internet.HasValue)return new EvidenceAssessment{MissingEvidence=true};
   DateTime start=login.Value>network.Value?login.Value:network.Value;
   if(login<boot||network<boot||process<boot||internet<process||internet<start)return new EvidenceAssessment{MissingEvidence=true};
   double ms=(internet.Value-start).TotalMilliseconds;return new EvidenceAssessment{ElapsedMs=ms,Passed=ms<=5000};
  }
 }
}
