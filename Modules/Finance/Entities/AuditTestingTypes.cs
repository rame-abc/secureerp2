using System;
using System.Collections.Generic;

namespace SecureERP2.Modules.Finance.Entities
{
    // 🔥 REAL ENTERPRISE AUDIT SYSTEM - Audit Testing Types for Big 4 Level
    
    public class RiskFactor
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string FactorName { get; set; } = string.Empty;
        public string FactorDescription { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string FactorCategory { get; set; } = string.Empty;
        public decimal RiskScore { get; set; }
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
        public string Likelihood { get; set; } = "Low";
        public string Impact { get; set; } = "Low";
        public string MitigationStrategy { get; set; } = string.Empty;
        public string ControlActivity { get; set; } = string.Empty;
        public string RiskOwner { get; set; } = string.Empty;
        public DateTime AssessmentDate { get; set; }
        public string AssessmentMethod { get; set; } = string.Empty;
        public string Evidence { get; set; } = string.Empty;
        public string MonitoringFrequency { get; set; } = string.Empty;
        public string NextReviewDate { get; set; } = string.Empty;
        public bool IsMitigated { get; set; }
        public string MitigationEffectiveness { get; set; } = string.Empty;
        public string AssessedBy { get; set; } = string.Empty;
        public string ReviewedBy { get; set; } = string.Empty;
        public DateTime? ReviewedAt { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public class AccountBalanceTest
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public DateTime TestDate { get; set; }
        public string TestType { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public string Assertion { get; set; } = string.Empty;
        public string TestMethod { get; set; } = string.Empty;
        public int PopulationSize { get; set; }
        public decimal SamplingInterval { get; set; }
        public string SelectionMethod { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public string TestObjective { get; set; } = string.Empty;
        public string SampleSelection { get; set; } = string.Empty;
        public int Deviations { get; set; }
        public decimal DeviationRate { get; set; }
        public string TestResult { get; set; } = "Pass";
        public string TestConclusion { get; set; } = string.Empty;
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
        public bool IsBalanceCorrect { get; set; }
        public string Explanation { get; set; } = string.Empty;
        public string AdjustmentsRequired { get; set; } = string.Empty;
        public decimal AdjustmentAmount { get; set; }
        public string TesterName { get; set; } = string.Empty;
        public string ReviewerName { get; set; } = string.Empty;
        public DateTime? ReviewedAt { get; set; }
        public string NextTestDate { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    public class TransactionTest
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public DateTime TestDate { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public string TestObjective { get; set; } = string.Empty;
        public string TestMethod { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public int TotalTransactions { get; set; }
        public bool Passed { get; set; }
        public int SampleSize { get; set; }
        public int Deviations { get; set; }
        public decimal DeviationRate { get; set; }
        public decimal TotalValue { get; set; }
        public decimal SampleValue { get; set; }
        public string TestResult { get; set; } = "Pass";
        public string TestConclusion { get; set; } = string.Empty;
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
        public List<string> DeviationDetails { get; set; } = new();
        public string ControlEffectiveness { get; set; } = string.Empty;
        public bool AreControlsEffective { get; set; }
        public string WeaknessesIdentified { get; set; } = string.Empty;
        public string Recommendations { get; set; } = string.Empty;
        public string TesterName { get; set; } = string.Empty;
        public string ReviewerName { get; set; } = string.Empty;
        public DateTime? ReviewedAt { get; set; }
        public string NextTestDate { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    public class CutoffTest
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public DateTime TestDate { get; set; }
        public string CutoffType { get; set; } = string.Empty;
        public DateTime CutoffDate { get; set; }
        public DateTime TestPeriodStart { get; set; }
        public DateTime TestPeriodEnd { get; set; }
        public string TestObjective { get; set; } = string.Empty;
        public string TestMethod { get; set; } = string.Empty;
        public int TransactionsBeforeCutoff { get; set; }
        public int TransactionsAfterCutoff { get; set; }
        public decimal AmountBeforeCutoff { get; set; }
        public decimal AmountAfterCutoff { get; set; }
        public int MisrecordedTransactions { get; set; }
        public decimal MisrecordedAmount { get; set; }
        public string TestResult { get; set; } = "Pass";
        public string TestConclusion { get; set; } = string.Empty;
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
        public bool Passed { get; set; }
        public List<string> CutoffViolations { get; set; } = new();
        public bool IsCutoffProper { get; set; }
        public string ControlEffectiveness { get; set; } = string.Empty;
        public string WeaknessesIdentified { get; set; } = string.Empty;
        public string Recommendations { get; set; } = string.Empty;
        public string TesterName { get; set; } = string.Empty;
        public string ReviewerName { get; set; } = string.Empty;
        public DateTime? ReviewedAt { get; set; }
        public string NextTestDate { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        
        // Additional properties needed by SAPAuditorService
        public int TransactionId { get; set; }
        public DateTime TransactionDate { get; set; }
        public bool IsInCorrectPeriod { get; set; }
        public bool HasProperDocumentation { get; set; }
        public string TestType { get; set; } = string.Empty;
    }

    public class TrendAnalysis
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public DateTime AnalysisDate { get; set; }
        public string AnalysisType { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public DateTime ComparisonPeriodStart { get; set; }
        public DateTime ComparisonPeriodEnd { get; set; }
        public decimal CurrentPeriodValue { get; set; }
        public decimal PriorPeriodValue { get; set; }
        public decimal BudgetValue { get; set; }
        public decimal VarianceAmount { get; set; }
        public decimal VariancePercentage { get; set; }
        public decimal GrowthRate { get; set; }
        public string TrendDirection { get; set; } = string.Empty;
        public string TrendSignificance { get; set; } = string.Empty;
        public List<string> TrendFactors { get; set; } = new();
        public List<string> Anomalies { get; set; } = new();
        public string AnalysisConclusion { get; set; } = string.Empty;
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
        public bool RequiresInvestigation { get; set; }
        public string InvestigationReasons { get; set; } = string.Empty;
        public string AnalystName { get; set; } = string.Empty;
        public string ReviewerName { get; set; } = string.Empty;
        public DateTime? ReviewedAt { get; set; }
        public string NextAnalysisDate { get; set; } = string.Empty;
        public string Recommendations { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        
        // Additional properties needed by SAPAuditorService
        public DateTime AnalysisStartedAt { get; set; }
        public DateTime AnalysisCompletedAt { get; set; }
        public bool HasErrors { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class RatioAnalysis
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public DateTime AnalysisDate { get; set; }
        public DateTime AnalysisStartedAt { get; set; }
        public DateTime AnalysisCompletedAt { get; set; }
        public decimal DebtToEquityRatio { get; set; }
        public decimal CurrentRatio { get; set; }
        public decimal QuickRatio { get; set; }
        public decimal ProfitMargin { get; set; }
        public decimal ReturnOnAssets { get; set; }
        public decimal ReturnOnEquity { get; set; }
        public decimal AssetTurnover { get; set; }
        public decimal InventoryTurnover { get; set; }
        public decimal ReceivablesTurnover { get; set; }
        public decimal WorkingCapitalRatio { get; set; }
        public string AnalysisConclusion { get; set; } = string.Empty;
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
        public bool HasAnomalies { get; set; }
        public bool HasErrors { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> Anomalies { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public string AnalystName { get; set; } = string.Empty;
        public string ReviewerName { get; set; } = string.Empty;
        public DateTime? ReviewedAt { get; set; }
        public string NextAnalysisDate { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    public class VarianceAnalysis
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public DateTime AnalysisDate { get; set; }
        public DateTime AnalysisStartedAt { get; set; }
        public DateTime AnalysisCompletedAt { get; set; }
        public List<VarianceData> VarianceData { get; set; } = new();
        public decimal TotalVarianceAmount { get; set; }
        public decimal TotalVariancePercentage { get; set; }
        public int SignificantVariances { get; set; }
        public string AnalysisConclusion { get; set; } = string.Empty;
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
        public bool HasAnomalies { get; set; }
        public bool HasErrors { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> Recommendations { get; set; } = new();
        public string AnalystName { get; set; } = string.Empty;
        public string ReviewerName { get; set; } = string.Empty;
        public DateTime? ReviewedAt { get; set; }
        public string NextAnalysisDate { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    public class VarianceData
    {
        public int AccountId { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public decimal BudgetAmount { get; set; }
        public decimal ActualAmount { get; set; }
        public decimal VarianceAmount { get; set; }
        public decimal VariancePercentage { get; set; }
        public bool IsSignificant { get; set; }
        public string VarianceExplanation { get; set; } = string.Empty;
        public string ActionRequired { get; set; } = string.Empty;
    }
}
