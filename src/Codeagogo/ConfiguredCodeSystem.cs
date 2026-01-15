// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

namespace Codeagogo;

/// <summary>
/// Represents a configured code system for terminology lookups.
/// </summary>
/// <param name="Uri">The code system URI (e.g., "http://snomed.info/sct")</param>
/// <param name="Title">Display name for the code system (e.g., "SNOMED CT")</param>
/// <param name="Enabled">Whether this code system is enabled for lookups</param>
public record ConfiguredCodeSystem(string Uri, string Title, bool Enabled);
