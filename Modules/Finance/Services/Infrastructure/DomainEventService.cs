using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using SecureERP2.Modules.Finance.Entities;

namespace SecureERP2.Modules.Finance.Services.Infrastructure
{
    /// <summary>
    /// 🔒 Centralized domain event service for versioning and validation
    /// </summary>
    public class DomainEventService
    {
        private readonly ILogger<DomainEventService> _logger;
        private readonly Dictionary<string, int> _eventVersions = new();

        public DomainEventService(ILogger<DomainEventService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Validate and process domain event
        /// </summary>
        public DomainEventResult ProcessDomainEvent(
            object eventData,
            string eventType,
            Guid eventId,
            int companyId,
            string correlationId = "")
        {
            var result = new DomainEventResult
            {
                IsValid = true,
                EventId = eventId,
                EventType = eventType,
                CompanyId = companyId,
                ProcessedAt = DateTime.UtcNow
            };

            try
            {
                // 🔥 Event versioning
                var currentVersion = GetNextEventVersion(eventType);
                result.EventVersion = currentVersion;

                // 🔥 Event validation
                ValidateEventStructure(eventData, eventType, result);

                // 🔥 Business rule validation
                ValidateBusinessRules(eventData, eventType, result);

                // 🔥 Event serialization validation
                ValidateEventSerialization(eventData, result);

                _logger.LogInformation(
                    "Domain event {EventType} v{EventVersion} processed successfully for {CorrelationId}", 
                    eventType, currentVersion, correlationId);

                result.IsValid = result.Errors.Count == 0;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Domain event processing failed: {ex.Message}");
                _logger.LogError(ex, 
                    "Domain event processing failed for {EventType} - {CorrelationId}", 
                    eventType, correlationId);
            }

            return result;
        }

        /// <summary>
        /// Get next version number for event type
        /// </summary>
        private int GetNextEventVersion(string eventType)
        {
            if (_eventVersions.ContainsKey(eventType))
            {
                _eventVersions[eventType]++;
            }
            else
            {
                _eventVersions[eventType] = 1;
            }

            return _eventVersions[eventType];
        }

        /// <summary>
        /// Validate event structure
        /// </summary>
        private void ValidateEventStructure(object eventData, string eventType, DomainEventResult result)
        {
            if (eventData == null)
            {
                result.Errors.Add("Event data cannot be null");
                return;
            }

            // 🔥 Event-specific structure validation
            switch (eventType)
            {
                case "JournalCreated":
                    ValidateJournalCreatedStructure(eventData, result);
                    break;
                case "JournalPosted":
                    ValidateJournalPostedStructure(eventData, result);
                    break;
                case "JournalVoided":
                    ValidateJournalVoidedStructure(eventData, result);
                    break;
                default:
                    result.Warnings.Add($"Unknown event type: {eventType}");
                    break;
            }
        }

        /// <summary>
        /// Validate business rules for events
        /// </summary>
        private void ValidateBusinessRules(object eventData, string eventType, DomainEventResult result)
        {
            switch (eventType)
            {
                case "JournalCreated":
                    ValidateJournalCreatedRules(eventData, result);
                    break;
                case "JournalPosted":
                    ValidateJournalPostedRules(eventData, result);
                    break;
                case "JournalVoided":
                    ValidateJournalVoidedRules(eventData, result);
                    break;
            }
        }

        /// <summary>
        /// Validate event serialization
        /// </summary>
        private void ValidateEventSerialization(object eventData, DomainEventResult result)
        {
            try
            {
                var json = JsonSerializer.Serialize(eventData);
                if (string.IsNullOrWhiteSpace(json))
                {
                    result.Errors.Add("Event data serialization failed");
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Event serialization error: {ex.Message}");
            }
        }

        #region Event-Specific Validation Methods

        private void ValidateJournalCreatedStructure(object eventData, DomainEventResult result)
        {
            if (!eventData.GetType().GetProperties().Any(p => p.Name == "JournalEntryId"))
            {
                result.Errors.Add("JournalCreated event missing required property: JournalEntryId");
            }
        }

        private void ValidateJournalPostedStructure(object eventData, DomainEventResult result)
        {
            if (!eventData.GetType().GetProperties().Any(p => p.Name == "JournalEntryId"))
            {
                result.Errors.Add("JournalPosted event missing required property: JournalEntryId");
            }
        }

        private void ValidateJournalVoidedStructure(object eventData, DomainEventResult result)
        {
            if (!eventData.GetType().GetProperties().Any(p => p.Name == "JournalEntryId"))
            {
                result.Errors.Add("JournalVoided event missing required property: JournalEntryId");
            }
        }

        private void ValidateJournalCreatedRules(object eventData, DomainEventResult result)
        {
            // 🔥 Business rules for JournalCreated events
            // Add specific business logic here
        }

        private void ValidateJournalPostedRules(object eventData, DomainEventResult result)
        {
            // 🔥 Business rules for JournalPosted events
            // Add specific business logic here
        }

        private void ValidateJournalVoidedRules(object eventData, DomainEventResult result)
        {
            // 🔥 Business rules for JournalVoided events
            // Add specific business logic here
        }

        #endregion
    }

    /// <summary>
    /// Domain event processing result
    /// </summary>
    public class DomainEventResult
    {
        public bool IsValid { get; set; }
        public Guid EventId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public int EventVersion { get; set; }
        public int CompanyId { get; set; }
        public DateTime ProcessedAt { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
