﻿// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Performance", "CA1862:Use the 'StringComparison' method overloads to perform case-insensitive string comparisons", Justification = "<Pending>", Scope = "member", Target = "~M:DevHabit.Api.Controllers.HabitsController.GetHabits(DevHabit.Api.DTOs.Habits.HabitsQueryParameters,DevHabit.Api.Services.Sorting.SortMappingProvider,DevHabit.Api.Services.DataShapingService)~System.Threading.Tasks.Task{Microsoft.AspNetCore.Mvc.IActionResult}")]
[assembly: SuppressMessage("Major Code Smell", "S2326:Unused type parameters should be removed", Justification = "<Pending>", Scope = "type", Target = "~T:DevHabit.Api.Services.Sorting.SortMappingDefinition`2")]
[assembly: SuppressMessage("Performance", "CA1849:Call async methods when in an async method", Justification = "<Pending>", Scope = "member", Target = "~M:DevHabit.Api.Jobs.ProcessEntryImportJob.Execute(Quartz.IJobExecutionContext)~System.Threading.Tasks.Task")]
