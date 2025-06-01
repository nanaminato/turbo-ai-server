using System.Text;
using iTextSharp.text;
using Microsoft.Extensions.Primitives;
using Turbo_Auth.Models.Ai.Chat;

namespace Turbo_Auth.Handlers.Group;

/*
 * 如果以initDefault = true进行初始化，将会获得默认的写死的
 * 可替代模型组，如果以initDefault=false进行初始化，则不会进行初始化
 * 这时，不会进行填充数据，可用于构建基于数据库的模型组对象
 * *
 */
public class ModelGroup
{
    private List<List<ChatDisplayModel>> _group = new();

    public ModelGroup(bool initDefault)
    {
        if (initDefault)
        {
            var list = new List<ChatDisplayModel>();
            _group.Add(list);
            list.Add(new("gpt-3.5-turbo"));
            list.Add(new("gpt-3.5-turbo-16k"));
            list.Add(new("gpt-3.5-turbo-1106"));
            list.Add(new("gpt-4"));
            list.Add(new("gpt-4-1106-preview"));
            list.Add(new("gpt-4-0613"));
            list.Add(new("gemini-pro"));
            list.Add(new("gemini-pro_o"));
            list.Add(new("claude-2.1"));
            list.Add(new("claude-3-opus-20240229"));
            list.Add(new("claude-3-sonnet-20240229"));
            list.Add(new("claude-3-haiku-20240307"));
        }
    }

    public List<List<ChatDisplayModel>> Group
    {
        get => _group;
    }
    public List<string?>? GetGroupModels(string model)
    {
        foreach (var group in _group)
        {
            var valueGroup = group.Select(c => c.ModelValue).ToList();
            if (valueGroup.Contains(model))
            {
                return group.Select(c => c.ModelValue).ToList();
            }
        }

        return null;
    }

    public void Clear()
    {
        _group.Clear();
    }

    public void AddGroup(List<ChatDisplayModel> models)
    {
        _group.Add(models);
    }

    public override string ToString()
    {
        var builder = new StringBuilder();
        var i = 0;
        foreach (var group in _group)
        {
            builder.Append($"group ${++i}\n");
            foreach (var item in group)
            {
                builder.Append($"{item} ");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }
}