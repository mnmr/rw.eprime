using System;
using System.Collections.Generic;

namespace WorkRoles.Core
{
    public interface IColonistSearchTarget
    {
        bool NameContains(string term);
        bool HasRoleContaining(string term);
        bool HasJobContaining(string term);
    }

    public sealed class ColonistSearchQuery
    {
        private enum Scope
        {
            Name,
            Role,
            Job,
        }

        private readonly struct Term
        {
            internal Term(Scope scope, string value)
            {
                Scope = scope;
                Value = value;
            }

            internal Scope Scope { get; }
            internal string Value { get; }
        }

        private static readonly char[] Whitespace = { ' ', '\t', '\r', '\n' };
        private static readonly ColonistSearchQuery Empty =
            new ColonistSearchQuery(Array.Empty<Term>());

        private readonly Term[] terms;

        private ColonistSearchQuery(Term[] terms)
        {
            this.terms = terms;
        }

        public bool IsEmpty => terms.Length == 0;

        public static ColonistSearchQuery Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Empty;

            string[] tokens = text!.Split(
                Whitespace, StringSplitOptions.RemoveEmptyEntries);
            var parsed = new List<Term>(tokens.Length);
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                Scope scope = Scope.Name;
                int prefixLength = 0;
                if (token.StartsWith("role:",
                        StringComparison.OrdinalIgnoreCase))
                {
                    scope = Scope.Role;
                    prefixLength = 5;
                }
                else if (token.StartsWith("r:",
                             StringComparison.OrdinalIgnoreCase))
                {
                    scope = Scope.Role;
                    prefixLength = 2;
                }
                else if (token.StartsWith("job:",
                             StringComparison.OrdinalIgnoreCase))
                {
                    scope = Scope.Job;
                    prefixLength = 4;
                }
                else if (token.StartsWith("j:",
                             StringComparison.OrdinalIgnoreCase))
                {
                    scope = Scope.Job;
                    prefixLength = 2;
                }

                string value = prefixLength == 0
                    ? token : token.Substring(prefixLength);
                if (value.Length > 0) parsed.Add(new Term(scope, value));
            }
            return parsed.Count == 0
                ? Empty : new ColonistSearchQuery(parsed.ToArray());
        }

        public bool Matches<TTarget>(TTarget target)
            where TTarget : IColonistSearchTarget
        {
            for (int i = 0; i < terms.Length; i++)
            {
                Term term = terms[i];
                bool matches;
                switch (term.Scope)
                {
                    case Scope.Role:
                        matches = target.HasRoleContaining(term.Value);
                        break;
                    case Scope.Job:
                        matches = target.HasJobContaining(term.Value);
                        break;
                    default:
                        matches = target.NameContains(term.Value);
                        break;
                }
                if (!matches) return false;
            }
            return true;
        }
    }
}
