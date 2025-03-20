﻿using System.Globalization;
using CsvHelper;
using DevHabit.Api.Database;
using DevHabit.Api.DTOs.EntryImports;
using DevHabit.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace DevHabit.Api.Jobs;

public sealed class ProcessEntryImportJob(
    ApplicationDbContext dbContext,
    ILogger<ProcessEntryImportJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        string importJobId = context.MergedJobDataMap.GetString("importJobId")!;

        EntryImportJob? importJob = await dbContext.EntryImportJobs
            .FirstOrDefaultAsync(j => j.Id == importJobId);

        if (importJob is null)
        {
            logger.LogError("Entry import job with ID {ImportJobId} not found", importJobId);
            return;
        }

        try
        {
            importJob.Status = EntryImportStatus.Processing;
            await dbContext.SaveChangesAsync();

            using var memoryStream = new MemoryStream(importJob.FileContent);
            using var reader = new StreamReader(memoryStream);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            var records = csv.GetRecords<CsvEntryRecord>().ToList();

            importJob.TotalRecords = records.Count;
            await dbContext.SaveChangesAsync();

            foreach (CsvEntryRecord record in records)
            {
                try
                {
                    Habit? habit = await dbContext.Habits
                        .FirstOrDefaultAsync(h => h.Id == record.HabitId && h.UserId == importJob.UserId);

                    if (habit is null)
                    {
                        throw new InvalidOperationException(
                            $"Habit with ID '{record.HabitId}' does not exist or does not belong to the user");
                    }

                    var entry = new Entry
                    {
                        Id = Entry.NewId(),
                        UserId = importJob.UserId,
                        HabitId = record.HabitId,
                        Value = habit.Target.Value,
                        Date = record.Date,
                        Notes = record.Notes,
                        Source = EntrySource.FileImport,
                        CreatedAtUtc = DateTime.UtcNow
                    };

                    dbContext.Entries.Add(entry);
                    importJob.SuccessfulRecords++;
                }
                catch (Exception ex)
                {
                    importJob.Errors.Add($"Error processing record: {ex.Message}");
                    importJob.FailedRecords++;

                    if (importJob.Errors.Count >= 100)
                    {
                        importJob.Errors.Add("Too many errors, stopping processing...");
                        break;
                    }
                }
                finally
                {
                    importJob.ProcessedRecords++;
                }

                if (importJob.ProcessedRecords % 100 == 0)
                {
                    await dbContext.SaveChangesAsync();
                }
            }

            importJob.Status = EntryImportStatus.Completed;
            importJob.CompletedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing import job {ImportJobId}", importJobId);

            importJob.Status = EntryImportStatus.Failed;
            importJob.Errors.Add($"Fatal error: {ex.Message}");
            importJob.CompletedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
        }
    }
}
