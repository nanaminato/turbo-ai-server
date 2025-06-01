using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using Turbo_Auth.Models.Accounts;

namespace Turbo_Auth.Models.Tasks;

public class GenerateTask
{
    [Key]
    [Required]
    [JsonProperty("task_id")]
    public string? TaskId
    {
        get;
        set;
    }
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

    [JsonProperty("dynamic")]
    public string? Dynamic
    {
        get;
        set;
    }
    [JsonIgnore]
    [ForeignKey("AccountId")]
    public Account? Account { get; set; }
    
}