using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
namespace LocalDrop
{
    class MySettings
    {
        public static Dictionary<string, object> ReadJsonToDictionary()
        {
            string filePath = "LocalDrop.json";
            try
            {
                // 读取文件内容
                string jsonString = File.ReadAllText(filePath);

                // 使用 System.Text.Json 反序列化为字典
                var dictionary = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString);

                return dictionary ?? new Dictionary<string, object>();
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine($"文件未找到: {filePath}");
                return new Dictionary<string, object>();
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON解析错误: {ex.Message}");
                return new Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发生错误: {ex.Message}");
                return new Dictionary<string, object>();
            }
        }

        public static void SaveDictionaryToJson(Dictionary<string, object> dictionary)
        {

            string filePath = "LocalDrop.json";
            try
            {
                // 设置 JSON 序列化选项
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true, // 是否格式化输出
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping // 处理中文等字符
                };

                // 序列化字典为 JSON 字符串
                string jsonString = JsonSerializer.Serialize(dictionary, options);

                // 写入文件
                File.WriteAllText(filePath, jsonString);

                Console.WriteLine($"字典已成功保存到: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存文件时发生错误: {ex.Message}");
            }
        }
    }
}
