
public class Invoice
{
    public string ReferenceMonth { get; set; }
    public string ReferenceYear { get; set; }
    public string Document { get; set; }
    public string Description { get; set; }
    public string Amount { get; set; }
    public string CreatedAt { get; set; }
    public string DeactivatedAt { get; set; }
    public Invoice(string ReferenceMonth, string ReferenceYear, string Document, string Description, string Amount, string CreatedAt, string DeactivatedAt)
    {
        this.ReferenceMonth = ReferenceMonth;
        this.ReferenceYear = ReferenceYear;
        this.Document = Document;
        this.Description = Description;
        this.Amount = Amount;
        this.CreatedAt = CreatedAt;
        this.DeactivatedAt = DeactivatedAt;
    }
}