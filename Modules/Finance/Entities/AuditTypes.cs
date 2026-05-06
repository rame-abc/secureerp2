using System;
using System.Collections.Generic;

namespace SecureERP2.Modules.Finance.Entities
{
    // 🔥 REAL ENTERPRISE AUDIT SYSTEM - Big 4 Level Audit Types
    
    public class FinancialStatementAudit
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public DateTime AuditPeriodStart { get; set; }
        public DateTime AuditPeriodEnd { get; set; }
        public string AuditType { get; set; } = "FinancialStatement";
        public DateTime AuditStartedAt { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public DateTime? AuditCompletedAt { get; set; }
        public string Status { get; set; } = "InProgress";
        public string AuditorName { get; set; } = string.Empty;
        public string ReviewerName { get; set; } = string.Empty;
        public List<string> Findings { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public decimal TotalRevenue { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetIncome { get; set; }
        public decimal TotalAssets { get; set; }
        public decimal TotalLiabilities { get; set; }
        public decimal TotalEquity { get; set; }
        public decimal Equity { get; set; }
        public bool HasMaterialMisstatements { get; set; }
        public bool HasFindings { get; set; }
        public bool HasErrors { get; set; }
        public string? MaterialMisstatementDetails { get; set; }
        public string? ErrorMessage { get; set; }
        public string AuditOpinion { get; set; } = "Unqualified";
        public string RiskAssessment { get; set; } = "Low";
        public bool IsCompliant { get; set; }
        public string? ComplianceIssues { get; set; }
        public DateTime? SignedAt { get; set; }
        public string? SignedBy { get; set; }
        public string Notes { get; set; } = string.Empty;
        
        // 🔥 Additional properties for SAPAuditorService
        public List<TrialBalanceData> TrialBalanceBalances { get; set; } = new();
        public List<BalanceSheetData> BalanceSheetEquationBalances { get; set; } = new();
        public List<CashFlowData> CashFlowReconciliations { get; set; } = new();
        public List<IncomeStatementData> IncomeStatementAnalyses { get; set; } = new();
        public List<EquityData> EquityAnalyses { get; set; } = new();
        public List<SegmentData> SegmentAnalyses { get; set; } = new();
    }
    
    public class TrialBalanceData
    {
        public int AccountId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal DebitBalance { get; set; }
        public decimal CreditBalance { get; set; }
        public decimal TrialBalance { get; set; }
        public bool IsBalanced { get; set; }
        public string AccountType { get; set; } = string.Empty;
    }
    
    public class BalanceSheetData
    {
        public string Category { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
        public decimal PriorYearAmount { get; set; }
        public decimal Variance { get; set; }
        public decimal VariancePercentage { get; set; }
    }
    
    public class CashFlowData
    {
        public string CashFlowType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsReconciled { get; set; }
        public decimal BankBalance { get; set; }
        public decimal BookBalance { get; set; }
        public decimal ReconciliationDifference { get; set; }
    }
    
    public class IncomeStatementData
    {
        public string LineItem { get; set; } = string.Empty;
        public decimal CurrentPeriod { get; set; }
        public decimal PriorPeriod { get; set; }
        public decimal Budget { get; set; }
        public decimal Variance { get; set; }
        public decimal VariancePercentage { get; set; }
        public bool IsMaterial { get; set; }
        public string Analysis { get; set; } = string.Empty;
    }
    
    public class EquityData
    {
        public string EquityComponent { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal PriorAmount { get; set; }
        public decimal Change { get; set; }
        public string ChangeReason { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
    }
    
    public class SegmentData
    {
        public string SegmentName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public decimal Expenses { get; set; }
        public decimal OperatingIncome { get; set; }
        public decimal Assets { get; set; }
        public string SegmentType { get; set; } = string.Empty;
        public bool IsReportable { get; set; }
    }

    public class InternalControlAudit
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public DateTime AuditDate { get; set; }
        public DateTime AuditStartedAt { get; set; }
        public string ControlType { get; set; } = string.Empty;
        public string ControlDescription { get; set; } = string.Empty;
        public string ControlObjective { get; set; } = string.Empty;
        public string TestMethod { get; set; } = string.Empty;
        public DateTime TestStartedAt { get; set; }
        public DateTime? TestCompletedAt { get; set; }
        public string TestResult { get; set; } = "Pass";
        public string Effectiveness { get; set; } = "Effective";
        public List<string> Deficiencies { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public int SampleSize { get; set; }
        public int DefectCount { get; set; }
        public decimal DefectRate { get; set; }
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
        public bool IsControlDesignEffective { get; set; }
        public bool IsControlOperatingEffective { get; set; }
        public string TesterName { get; set; } = string.Empty;
        public string ReviewerName { get; set; } = string.Empty;
        public DateTime? ReviewedAt { get; set; }
        public string NextTestDate { get; set; } = string.Empty;
        public string RemediationPlan { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        
        // 🔥 Additional properties for SAPAuditorService
        public List<SegregationIssue> SegregationOfDutiesIssues { get; set; } = new();
        public int UnapprovedTransactions { get; set; }
        public bool ApprovalControlIssues { get; set; }
        public int PeriodClosingProcedures { get; set; }
        public bool MissingPeriodClosings { get; set; }
        public int ActiveSystemUsers { get; set; }
        public int AdminUsers { get; set; }
        public bool TooManyAdmins { get; set; }
        public string OverallControlEffectiveness { get; set; } = string.Empty;
        public DateTime? AuditCompletedAt { get; set; }
        public bool HasDeficiencies { get; set; }
        public bool HasErrors { get; set; }
        public string? ErrorMessage { get; set; }
        
        // 🔥 Additional properties for SAPAuditorService
        public List<ControlTest> ControlTests { get; set; } = new();
        public List<ControlDeficiency> ControlDeficiencies { get; set; } = new();
        public List<ControlMatrix> ControlMatrix { get; set; } = new();
        public List<RiskAssessment> RiskAssessments { get; set; } = new();
        public List<ComplianceTest> ComplianceTests { get; set; } = new();
    }
    
    public class SegregationIssue
    {
        public string UserId { get; set; } = string.Empty;
        public int TransactionCount { get; set; }
        public decimal TotalAmount { get; set; }
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
        public string Description { get; set; } = string.Empty;
        public DateTime IdentifiedAt { get; set; }
        public string IdentifiedBy { get; set; } = string.Empty;
        public string Status { get; set; } = "Open";
        public string Recommendation { get; set; } = string.Empty;
    }

    public class ControlTest
    {
        public int Id { get; set; }
        public string ControlName { get; set; } = string.Empty;
        public string TestDescription { get; set; } = string.Empty;
        public string TestResult { get; set; } = "Pass";
        public string TestEvidence { get; set; } = string.Empty;
        public DateTime TestDate { get; set; }
        public string Tester { get; set; } = string.Empty;
        public bool IsEffective { get; set; }
        public string Weakness { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
    }
    
    public class ControlDeficiency
    {
        public int Id { get; set; }
        public string DeficiencyType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = "Low";
        public string Impact { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
        public DateTime IdentifiedDate { get; set; }
        public string IdentifiedBy { get; set; } = string.Empty;
        public string Status { get; set; } = "Open";
        public DateTime? RemediationDate { get; set; }
        public string RemediationAction { get; set; } = string.Empty;
    }
    
    public class ControlMatrix
    {
        public string Process { get; set; } = string.Empty;
        public string Control { get; set; } = string.Empty;
        public string Risk { get; set; } = string.Empty;
        public string ControlType { get; set; } = string.Empty;
        public string Frequency { get; set; } = string.Empty;
        public string Effectiveness { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Documentation { get; set; } = string.Empty;
        public bool IsAutomated { get; set; }
        public string TestResults { get; set; } = string.Empty;
    }

    public class ComplianceAudit
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public DateTime AuditPeriodStart { get; set; }
        public DateTime AuditPeriodEnd { get; set; }
        public DateTime AuditStartedAt { get; set; }
        public string ComplianceType { get; set; } = string.Empty;
        public string Regulation { get; set; } = string.Empty;
        public string Standard { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Status { get; set; } = "InProgress";
        public List<string> ComplianceRequirements { get; set; } = new();
        public List<ComplianceTest> Tests { get; set; } = new();
        public List<string> Violations { get; set; } = new();
        public List<string> RemediationActions { get; set; } = new();
        public bool IsCompliant { get; set; }
        public decimal ComplianceScore { get; set; }
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
        public string AuditorName { get; set; } = string.Empty;
        public string ReviewerName { get; set; } = string.Empty;
        public DateTime? ReviewedAt { get; set; }
        public string NextAuditDate { get; set; } = string.Empty;
        public string CertificationStatus { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        
        // 🔥 Additional properties for SAPAuditorService
        public int TaxCalculationsPerformed { get; set; }
        public bool TaxComplianceIssues { get; set; }
        public int RegulatoryTransactions { get; set; }
        public bool RegulatoryComplianceIssues { get; set; }
        public bool DataRetentionIssues { get; set; }
        public int OldTransactionsCount { get; set; }
        public int AuditTrailEntries { get; set; }
        public int TotalTransactions { get; set; }
        public double AuditTrailCompleteness { get; set; }
        public bool AuditTrailIssues { get; set; }
        public DateTime? AuditCompletedAt { get; set; }
        public bool HasComplianceIssues { get; set; }
        public bool HasErrors { get; set; }
        public string? ErrorMessage { get; set; }
        
        // 🔥 Additional properties for SAPAuditorService
        public List<ComplianceRequirement> ComplianceMatrix { get; set; } = new();
        public List<ComplianceViolation> ComplianceViolations { get; set; } = new();
        public List<ComplianceEvidence> ComplianceEvidence { get; set; } = new();
        public List<ComplianceReport> ComplianceReports { get; set; } = new();
        public List<ComplianceMetric> ComplianceMetrics { get; set; } = new();
    }
    
    public class ComplianceRequirement
    {
        public int Id { get; set; }
        public string RequirementId { get; set; } = string.Empty;
        public string RequirementDescription { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Status { get; set; } = "Compliant";
        public string Evidence { get; set; } = string.Empty;
        public string TestMethod { get; set; } = string.Empty;
        public DateTime TestDate { get; set; }
        public string Tester { get; set; } = string.Empty;
        public string Findings { get; set; } = string.Empty;
        public string Recommendations { get; set; } = string.Empty;
        public bool IsCritical { get; set; }
        public string DueDate { get; set; } = string.Empty;
    }
    
    public class ComplianceViolation
    {
        public int Id { get; set; }
        public string ViolationType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = "Low";
        public string Regulation { get; set; } = string.Empty;
        public string Standard { get; set; } = string.Empty;
        public DateTime IdentifiedDate { get; set; }
        public string IdentifiedBy { get; set; } = string.Empty;
        public string Status { get; set; } = "Open";
        public DateTime? RemediationDate { get; set; }
        public string RemediationAction { get; set; } = string.Empty;
        public decimal PenaltyAmount { get; set; }
        public string Impact { get; set; } = string.Empty;
    }
    
    public class ComplianceEvidence
    {
        public int Id { get; set; }
        public string EvidenceType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime CollectionDate { get; set; }
        public string CollectedBy { get; set; } = string.Empty;
        public string Status { get; set; } = "Valid";
        public string Review { get; set; } = string.Empty;
        public bool IsSufficient { get; set; }
        public string Gap { get; set; } = string.Empty;
    }
    
    public class ComplianceReport
    {
        public int Id { get; set; }
        public string ReportType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime ReportDate { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string Findings { get; set; } = string.Empty;
        public string Recommendations { get; set; } = string.Empty;
        public string Conclusion { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Status { get; set; } = "Draft";
        public string Version { get; set; } = string.Empty;
    }
    
    public class ComplianceMetric
    {
        public int Id { get; set; }
        public string MetricName { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string Status { get; set; } = "On Track";
        public DateTime MeasurementDate { get; set; }
        public string Trend { get; set; } = string.Empty;
        public string Analysis { get; set; } = string.Empty;
    }

    public class ComplianceTest
    {
        public string TestName { get; set; } = string.Empty;
        public string TestDescription { get; set; } = string.Empty;
        public string ExpectedResult { get; set; } = string.Empty;
        public string ActualResult { get; set; } = string.Empty;
        public string Status { get; set; } = "Pass";
        public string Evidence { get; set; } = string.Empty;
        public DateTime TestDate { get; set; }
        public string Tester { get; set; } = string.Empty;
    }

    public class RiskAssessment
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public DateTime AssessmentDate { get; set; }
        public DateTime AssessmentStartedAt { get; set; }
        public string RiskCategory { get; set; } = string.Empty;
        public string RiskDescription { get; set; } = string.Empty;
        public string RiskSource { get; set; } = string.Empty;
        public decimal InherentRiskScore { get; set; }
        public decimal ControlRiskScore { get; set; }
        public decimal DetectionRiskScore { get; set; }
        public decimal ResidualRiskScore { get; set; }
        public string InherentRisk { get; set; } = "Low";
        public string ControlRisk { get; set; } = "Low";
        public string DetectionRisk { get; set; } = "Low";
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
        public string Likelihood { get; set; } = "Low";
        public string Impact { get; set; } = "Low";
        public List<string> RiskFactors { get; set; } = new();
        public List<string> MitigationStrategies { get; set; } = new();
        public List<string> ControlActivities { get; set; } = new();
        public bool IsAcceptable { get; set; }
        public string RiskOwner { get; set; } = string.Empty;
        public string ReviewerName { get; set; } = string.Empty;
        public DateTime? ReviewedAt { get; set; }
        public string NextReviewDate { get; set; } = string.Empty;
        public string ActionPlan { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        
        // 🔥 Additional properties for SAPAuditorService
        public DateTime? AssessmentCompletedAt { get; set; }
        public bool HasErrors { get; set; }
        public bool HasHighRiskFactors { get; set; }
        public string? ErrorMessage { get; set; }
        public string OverallRisk { get; set; } = string.Empty;
        
        // 🔥 Additional properties for SAPAuditorService
        public List<RiskFactor> RiskFactorsList { get; set; } = new();
        public List<RiskMatrix> RiskMatrix { get; set; } = new();
        public List<RiskMitigation> RiskMitigations { get; set; } = new();
        public List<RiskIndicator> RiskIndicators { get; set; } = new();
        public List<RiskTrend> RiskTrends { get; set; } = new();
        public decimal HeatMapScore { get; set; }
        public string RiskHeatMap { get; set; } = string.Empty;
        public List<string> RiskCategories { get; set; } = new();
    }
    
    // RiskFactor is already defined in AdditionalAuditTypes.cs
    
    public class RiskMatrix
    {
        public string RiskCategory { get; set; } = string.Empty;
        public string Likelihood { get; set; } = string.Empty;
        public string Impact { get; set; } = string.Empty;
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
        public decimal Score { get; set; }
        public string Color { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Timeline { get; set; } = string.Empty;
    }
    
    public class RiskMitigation
    {
        public int Id { get; set; }
        public string MitigationName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string RiskCategory { get; set; } = string.Empty;
        public string Effectiveness { get; set; } = string.Empty;
        public string Cost { get; set; } = string.Empty;
        public string Timeline { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Status { get; set; } = "Planned";
        public DateTime ImplementationDate { get; set; }
        public string Results { get; set; } = string.Empty;
    }
    
    public class RiskIndicator
    {
        public int Id { get; set; }
        public string IndicatorName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Threshold { get; set; } = string.Empty;
        public decimal CurrentValue { get; set; }
        public decimal TargetValue { get; set; }
        public string Status { get; set; } = "Normal";
        public string Trend { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; }
        public string AlertLevel { get; set; } = string.Empty;
    }
    
    public class RiskTrend
    {
        public int Id { get; set; }
        public string RiskCategory { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public decimal RiskScore { get; set; }
        public string Trend { get; set; } = string.Empty;
        public decimal Change { get; set; }
        public string Analysis { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
    }

    public class SubstantiveTesting
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public DateTime TestingStartedAt { get; set; }
        public DateTime TestDate { get; set; }
        public string TestType { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public string Assertion { get; set; } = string.Empty;
        public string TestMethod { get; set; } = string.Empty;
        public int SampleSize { get; set; }
        public int PopulationSize { get; set; }
        public decimal SamplingInterval { get; set; }
        public string SelectionMethod { get; set; } = string.Empty;
        public List<SubstantiveTestResult> Results { get; set; } = new();
        public string TestConclusion { get; set; } = "Pass";
        public decimal MisstatementAmount { get; set; }
        public decimal TolerableMisstatement { get; set; }
        public bool IsMaterialMisstatement { get; set; }
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
        public string TesterName { get; set; } = string.Empty;
        public string ReviewerName { get; set; } = string.Empty;
        public DateTime? ReviewedAt { get; set; }
        public string NextTestDate { get; set; } = string.Empty;
        public string Recommendations { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        
        // 🔥 Additional properties for SAPAuditorService
        public List<AccountBalanceTest> AccountBalanceTests { get; set; } = new();
        public List<TransactionTest> TransactionTests { get; set; } = new();
        public List<CutoffTest> CutoffTests { get; set; } = new();
        
        // 🔥 Additional properties for SAPAuditorService
        public int TotalTestsPerformed { get; set; }
        public bool TestsPassed { get; set; }
        public bool TestsFailed { get; set; }
        public DateTime TestingCompletedAt { get; set; }
        public bool HasExceptions { get; set; }
        public bool HasErrors { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class SubstantiveTestResult
    {
        public string ItemDescription { get; set; } = string.Empty;
        public string ItemReference { get; set; } = string.Empty;
        public decimal BookAmount { get; set; }
        public decimal AuditAmount { get; set; }
        public decimal Difference { get; set; }
        public string Status { get; set; } = "Match";
        public string Explanation { get; set; } = string.Empty;
        public string Evidence { get; set; } = string.Empty;
        public bool IsAdjusted { get; set; }
        public string AdjustmentReason { get; set; } = string.Empty;
    }
}
