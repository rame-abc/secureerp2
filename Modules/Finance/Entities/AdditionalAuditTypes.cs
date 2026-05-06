using System;
using System.Collections.Generic;

namespace SecureERP2.Modules.Finance.Entities
{
    // 🔥 REAL ENTERPRISE AUDIT SYSTEM - Additional Audit Types for Big 4 Level
    
    public enum RiskLevel
    {
        Low,
        Medium,
        High,
        Critical
    }
    
    public class AnalyticalProcedures
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public DateTime ProcedureDate { get; set; }
        public string ProcedureType { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public string PeriodType { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public decimal CurrentPeriodAmount { get; set; }
        public decimal PriorPeriodAmount { get; set; }
        public decimal ExpectedAmount { get; set; }
        public decimal VarianceAmount { get; set; }
        public decimal VariancePercentage { get; set; }
        public string VarianceAnalysis { get; set; } = string.Empty;
        public List<UnusualFluctuation> Fluctuations { get; set; } = new();
        public List<AccountBalance> Balances { get; set; } = new();
        public string ProcedureConclusion { get; set; } = "Normal";
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
        public bool RequiresInvestigation { get; set; }
        public string InvestigationReasons { get; set; } = string.Empty;
        public string AnalystName { get; set; } = string.Empty;
        public string ReviewerName { get; set; } = string.Empty;
        public DateTime? ReviewedAt { get; set; }
        public string NextProcedureDate { get; set; } = string.Empty;
        public string Recommendations { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        
        // 🔥 Additional properties for SAPAuditorService
        public DateTime ProceduresStartedAt { get; set; }
        public TrendAnalysis TrendAnalysis { get; set; }
        public RatioAnalysis RatioAnalysis { get; set; }
        public DateTime ProceduresCompletedAt { get; set; }
        public bool HasAnomalies { get; set; }
        public bool HasErrors { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class UnusualFluctuation
    {
        public int Id { get; set; }
        public int AnalyticalProcedureId { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public decimal CurrentPeriodValue { get; set; }
        public decimal PriorPeriodValue { get; set; }
        public decimal ExpectedValue { get; set; }
        public decimal VarianceAmount { get; set; }
        public decimal VariancePercentage { get; set; }
        public string FluctuationType { get; set; } = string.Empty;
        public string Significance { get; set; } = "Low";
        public string Explanation { get; set; } = string.Empty;
        public string InvestigationRequired { get; set; } = "No";
        public string InvestigationResults { get; set; } = string.Empty;
        public bool IsReasonable { get; set; }
        public string SupportingEvidence { get; set; } = string.Empty;
        public DateTime FluctuationDate { get; set; }
        public string DetectedBy { get; set; } = string.Empty;
    }

    
    public class AccountBalance
    {
        public int Id { get; set; }
        public int AnalyticalProcedureId { get; set; }
        public int AccountId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public decimal BalanceAmount { get; set; }
        public decimal ExpectedBalance { get; set; }
        public decimal VarianceAmount { get; set; }
        public decimal VariancePercentage { get; set; }
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public int TransactionCount { get; set; }
        public string BalanceType { get; set; } = string.Empty;
        public DateTime BalanceDate { get; set; }
        public string Currency { get; set; } = string.Empty;
        public decimal ExchangeRate { get; set; }
        public decimal ForeignBalance { get; set; }
        public string ReconciliationStatus { get; set; } = string.Empty;
        public string ReconciliationNotes { get; set; } = string.Empty;
        public bool IsReconciled { get; set; }
        public DateTime? ReconciledAt { get; set; }
        public string? ReconciledBy { get; set; }
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
        public string Comments { get; set; } = string.Empty;
    }

    public class AuditOpinion
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public DateTime OpinionDate { get; set; }
        public string OpinionType { get; set; } = string.Empty;
        public string OpinionDescription { get; set; } = string.Empty;
        public string BasisForOpinion { get; set; } = string.Empty;
        public string KeyAuditMatters { get; set; } = string.Empty;
        public string EmphasisOfMatter { get; set; } = string.Empty;
        public string OtherMatter { get; set; } = string.Empty;
        public string QualifiedOpinionReason { get; set; } = string.Empty;
        public string AdverseOpinionReason { get; set; } = string.Empty;
        public string DisclaimerReason { get; set; } = string.Empty;
        public string AuditorName { get; set; } = string.Empty;
        public string AuditorFirm { get; set; } = string.Empty;
        public string AuditorSignature { get; set; } = string.Empty;
        public DateTime? SignedAt { get; set; }
        public string ReviewerName { get; set; } = string.Empty;
        public DateTime? ReviewedAt { get; set; }
        public string ReportNumber { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    public class AuditReport
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public DateTime ReportDate { get; set; }
        public string ReportType { get; set; } = string.Empty;
        public string ReportTitle { get; set; } = string.Empty;
        public string ExecutiveSummary { get; set; } = string.Empty;
        public string Findings { get; set; } = string.Empty;
        public string Recommendations { get; set; } = string.Empty;
        public string Conclusion { get; set; } = string.Empty;
        public string RiskAssessment { get; set; } = string.Empty;
        public string ComplianceStatus { get; set; } = string.Empty;
        public string FinancialHighlights { get; set; } = string.Empty;
        public string OperationalHighlights { get; set; } = string.Empty;
        public string KeyMetrics { get; set; } = string.Empty;
        public string Trends { get; set; } = string.Empty;
        public string Anomalies { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public string ReviewerName { get; set; } = string.Empty;
        public DateTime? ReviewedAt { get; set; }
        public string ReportStatus { get; set; } = "Draft";
        public string Version { get; set; } = string.Empty;
        public string DistributionList { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    public class FraudDetection
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public DateTime DetectionDate { get; set; }
        public string DetectionType { get; set; } = string.Empty;
        public string RiskPattern { get; set; } = string.Empty;
        public string AnomalyDescription { get; set; } = string.Empty;
        public decimal AnomalyAmount { get; set; }
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
        public string FraudIndicator { get; set; } = string.Empty;
        public string DetectionMethod { get; set; } = string.Empty;
        public string Evidence { get; set; } = string.Empty;
        public string InvestigationStatus { get; set; } = string.Empty;
        public string InvestigationResults { get; set; } = string.Empty;
        public bool IsConfirmedFraud { get; set; }
        public string FraudType { get; set; } = string.Empty;
        public string Perpetrator { get; set; } = string.Empty;
        public decimal LossAmount { get; set; }
        public string RecoveryStatus { get; set; } = string.Empty;
        public string PreventionMeasures { get; set; } = string.Empty;
        public string DetectedBy { get; set; } = string.Empty;
        public string ReviewedBy { get; set; } = string.Empty;
        public DateTime? ReviewedAt { get; set; }
        public string NextReviewDate { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    public class AIAnomalyDetection
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public DateTime DetectionDate { get; set; }
        public string ModelName { get; set; } = string.Empty;
        public string ModelVersion { get; set; } = string.Empty;
        public string AnomalyType { get; set; } = string.Empty;
        public string AnomalyDescription { get; set; } = string.Empty;
        public decimal ConfidenceScore { get; set; }
        public decimal AnomalyScore { get; set; }
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
        public string DataPoints { get; set; } = string.Empty;
        public string Pattern { get; set; } = string.Empty;
        public string Features { get; set; } = string.Empty;
        public string Threshold { get; set; } = string.Empty;
        public string AlertStatus { get; set; } = string.Empty;
        public string InvestigationRequired { get; set; } = string.Empty;
        public string InvestigationResults { get; set; } = string.Empty;
        public bool IsFalsePositive { get; set; }
        public string FalsePositiveReason { get; set; } = string.Empty;
        public string ModelAccuracy { get; set; } = string.Empty;
        public string TrainingData { get; set; } = string.Empty;
        public string DetectedBy { get; set; } = string.Empty;
        public string ReviewedBy { get; set; } = string.Empty;
        public DateTime? ReviewedAt { get; set; }
        public string NextModelUpdate { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }
}
