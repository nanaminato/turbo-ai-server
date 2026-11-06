using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using RelaxKon.Auth.Models.Accounts;

namespace RelaxKon.Auth.Models.Sync.Tasks;

public class GenerateTask
{
    [Key]
    [Required]
    [MaxLength(255)]
    [JsonProperty("task_id")]
    public string TaskId
    {
        get;
        set;
    } = string.Empty;
    [JsonProperty("task_type")]
    public string? TaskType
    {
        get;
        set;
    }
    [JsonProperty("date")]
    public DateTime? DateTime
    {
        get;
        set;
    }
    [JsonProperty("account_id")]
    public int AccountId { get; set; }

    [JsonProperty("images")]
    public List<string>? Images { get; set; }
    
    [JsonProperty("videos")]
    public List<string>? Videos { get; set; }
    
    [JsonIgnore]
    [ForeignKey("AccountId")]
    public Account? Account { get; set; }
    
}