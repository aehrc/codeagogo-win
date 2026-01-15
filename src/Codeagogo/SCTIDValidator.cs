// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

namespace Codeagogo;

/// <summary>
/// Validates SNOMED CT Identifiers using the Verhoeff check digit algorithm.
/// </summary>
public static class SCTIDValidator
{
    // D5 dihedral group multiplication table
    private static readonly int[,] d = new int[10, 10]
    {
        {0,1,2,3,4,5,6,7,8,9},
        {1,2,3,4,0,6,7,8,9,5},
        {2,3,4,0,1,7,8,9,5,6},
        {3,4,0,1,2,8,9,5,6,7},
        {4,0,1,2,3,9,5,6,7,8},
        {5,9,8,7,6,0,4,3,2,1},
        {6,5,9,8,7,1,0,4,3,2},
        {7,6,5,9,8,2,1,0,4,3},
        {8,7,6,5,9,3,2,1,0,4},
        {9,8,7,6,5,4,3,2,1,0}
    };

    // Permutation table for Verhoeff algorithm
    private static readonly int[,] p = new int[8, 10]
    {
        {0,1,2,3,4,5,6,7,8,9},
        {1,5,7,6,2,8,3,0,9,4},
        {5,8,0,3,7,9,6,1,4,2},
        {8,9,1,6,0,4,3,5,2,7},
        {9,4,5,3,1,2,6,8,7,0},
        {4,2,8,6,5,7,3,9,0,1},
        {2,7,9,3,8,0,6,4,1,5},
        {7,0,4,6,9,1,3,2,5,8}
    };

    /// <summary>
    /// Validates a SNOMED CT Identifier using the Verhoeff check digit algorithm.
    /// </summary>
    /// <param name="candidate">The string to validate as a SCTID</param>
    /// <returns>True if the candidate is a valid SCTID, false otherwise</returns>
    public static bool IsValidSCTID(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        if (candidate.Length < 6 || candidate.Length > 18)
            return false;

        if (!candidate.All(char.IsDigit))
            return false;

        return VerhoeffCheck(candidate);
    }

    /// <summary>
    /// Checks whether a valid SCTID uses the short format (no namespace), indicating
    /// it is a core International Edition concept.
    /// </summary>
    /// <remarks>
    /// SNOMED CT identifiers encode a partition identifier in the 2nd and 3rd last digits.
    /// Short-format IDs have '0' as the 3rd-last digit (no namespace embedded),
    /// meaning they originate from the International Edition. Long-format IDs have '1'
    /// as the 3rd-last digit and include a 7-digit namespace identifier.
    /// </remarks>
    /// <param name="sctid">A validated SCTID string (must pass <see cref="IsValidSCTID"/> first)</param>
    /// <returns>True if the SCTID is short-format (International/core), false if namespaced</returns>
    public static bool IsCoreSCTID(string sctid)
    {
        if (sctid.Length < 6) return false;
        return sctid[^3] == '0';
    }

    /// <summary>
    /// Performs the Verhoeff check digit validation.
    /// </summary>
    private static bool VerhoeffCheck(string num)
    {
        var digits = num.Reverse().Select(c => c - '0').ToArray();
        int c = 0;

        for (int i = 0; i < digits.Length; i++)
        {
            c = d[c, p[i % 8, digits[i]]];
        }

        return c == 0;
    }
}
