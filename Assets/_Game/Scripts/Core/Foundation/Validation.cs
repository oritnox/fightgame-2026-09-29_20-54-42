// /Assets/_Game/Scripts/Core/Foundation/Validation.cs
// 공용코드 수정: F006 검증 값과 수치 경계. 모든 순수 코어 계약에 적용.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RP.Core.Foundation
{
    public enum ValidationSeverity { Warning, Error }

    public sealed class ValidationIssue
    {
        public string Code { get; }
        public string Path { get; }
        public string Expected { get; }
        public string Actual { get; }
        public ValidationSeverity Severity { get; }
        public ValidationIssue(string code, string path, string expected, string actual,
            ValidationSeverity severity = ValidationSeverity.Error)
        {
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Issue code is required.", nameof(code));
            if (path == null || expected == null || actual == null) throw new ArgumentNullException("Issue fields");
            if (!Enum.IsDefined(typeof(ValidationSeverity), severity)) throw new ArgumentOutOfRangeException(nameof(severity));
            Code = code; Path = path; Expected = expected; Actual = actual; Severity = severity;
        }
    }

    public sealed class ValidationReport
    {
        public ReadOnlyCollection<ValidationIssue> Issues { get; }
        public bool IsValid { get; }
        public ValidationReport(IEnumerable<ValidationIssue> issues)
        {
            if (issues == null) throw new ArgumentNullException(nameof(issues));
            var copy = new List<ValidationIssue>();
            bool valid = true;
            foreach (var issue in issues)
            {
                if (issue == null) throw new ArgumentException("Null issue.", nameof(issues));
                copy.Add(issue);
                if (issue.Severity == ValidationSeverity.Error) valid = false;
            }
            Issues = copy.AsReadOnly(); IsValid = valid;
        }
    }

    public static class NumericGuard
    {
        public static double Finite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(name, "Finite value required.");
            return value;
        }
        public static double NonNegative(double value, string name)
        {
            Finite(value, name);
            if (value < 0) throw new ArgumentOutOfRangeException(name, "Non-negative value required.");
            return value;
        }
        public static double Positive(double value, string name)
        {
            Finite(value, name);
            if (value <= 0) throw new ArgumentOutOfRangeException(name, "Positive value required.");
            return value;
        }
    }
}
