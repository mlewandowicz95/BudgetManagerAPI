using BudgetManagerAPI.Interfaces;
using BudgetManagerAPI.Models;
using QuestPDF.Fluent;

namespace BudgetManagerAPI.Services
{
    public class PdfReportService : IPdfReportService
    {
        public byte[] GenerateMonthlyBudgetReport(List<MonthlyBudget> budgets, User user)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(20);
                    page.Header().Text($"Raport budżetów miesięcznych użytkownika: {user.Email}").FontSize(20).Bold();
                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Kategoria").Bold();
                            header.Cell().Text("Limit").Bold();
                        });

                        foreach (var budget in budgets)
                        {
                            table.Cell().Text(budget.Category.Name);
                            table.Cell().Text(budget.Amount.ToString("C"));
                        }
                    });
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateTransactionReport(List<Transaction> transactions, User user)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(20);
                    page.Header().Text($"Raport transakcji użytkownika: {user.Email}").FontSize(20).Bold();
                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Data").Bold();
                            header.Cell().Text("Kategoria").Bold();
                            header.Cell().Text("Kwota").Bold();
                            header.Cell().Text("Typ").Bold();
                            header.Cell().Text("Opis").Bold();
                        });

                        foreach (var transaction in transactions)
                        {
                            table.Cell().Text(transaction.Date.ToString("yyyy-MM-dd"));
                            table.Cell().Text(transaction.Category.Name);
                            table.Cell().Text(transaction.Amount.ToString("C"));
                            table.Cell().Text(transaction.Type.ToString());
                            table.Cell().Text(transaction.Description ?? "-");
                        }
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}
