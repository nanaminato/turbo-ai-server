using NovitaModels;

var sql = new SqlGenerator();
var basePath = "你想要存储sql文件的resources的父路径";
// 示例
// var basePath = "C:\\Users\\betha\\Desktop\\毕业设计代码\\graduation-design-server\\NovitaModels\\";
var controller = new NovitaController();
foreach (var model in await controller.GetLoras())
{
    Console.WriteLine(model);
}

// 取消下面注释生成sql文件

// var vaeSql = basePath + "resources\\\\" + "vae.sql";
// await File.WriteAllTextAsync(vaeSql,sql.Generate(await controller.GetVaeModels()));
//
// var loraSql = basePath + "resources\\\\" + "lora.sql";
// await File.WriteAllTextAsync(loraSql,sql.Generate(await controller.GetLoras()));
//
// var embeddingSql = basePath + "resources\\\\" + "embedding.sql";
// await File.WriteAllTextAsync(embeddingSql,sql.Generate(await controller.GetEmbeddings()));
//
// var imageSql = basePath + "resources\\\\" + "image.sql";
// await File.WriteAllTextAsync(imageSql,sql.Generate(await controller.GetImageModels()));