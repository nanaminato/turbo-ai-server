using System.Text;

namespace NovitaModels;

public class SqlGenerator
{
    public string Generate(List<NovitaModel> models)
    {
        var builder = new StringBuilder();
        builder.Append("insert into NovitaModels(model, cover, type, nsfw, sdxl) values");
        foreach (var model in models)
        {
            builder.Append($"('{SafeCheck(model.Model!)}','{model.Cover}','{model.Type}',{model.Nsfw},{model.Sdxl}),");
            // builder.Append($"(`{model.Model}`,`{model.Cover}`,`{model.Type}`,{model.Nsfw},{model.Sdxl}),");
        }

        builder.Remove(builder.Length - 1, 1);
        builder.Append(";");
        return builder.ToString();
    }

    string SafeCheck(string model)
    {
        if (model.Contains('\''))
        {
            model = model.Replace("'", "\\'");
        }
        return model;
    }
}