#!/bin/bash

# 设置源目录和目标目录
PROTO_DIR="Assets/Scripts/NetworkFramework/Models/Proto"
OUTPUT_DIR="Assets/Scripts/NetworkFramework/Models/Generated"

# 函数：将字符串转换为PascalCase
function to_pascal_case() {
    # 将下划线/连字符分隔的单词转换为PascalCase
    # 例如: my_file -> MyFile, user-data -> UserData, http_request -> HttpRequest
    echo "$1" | awk -F'[-_]' '{ 
        result = ""; 
        for (i=1; i<=NF; i++) {
            result = result toupper(substr($i,1,1)) substr($i,2); 
        } 
        print result; 
    }'
}

# 确保输出目录存在
mkdir -p "$OUTPUT_DIR"

# 查找所有.proto文件，但排除google/protobuf目录下的文件
# 这些文件是Google Protobuf的标准类型，已经包含在Google.Protobuf.WellKnownTypes中
proto_files=$(find "$PROTO_DIR" -name "*.proto" | grep -v "google/protobuf")

echo "Excluding google/protobuf directory to avoid conflicts with Google.Protobuf.WellKnownTypes"

# 为每个.proto文件生成C#代码
for proto_file in $proto_files; do
    # 计算相对于Proto目录的路径
    rel_path=$(echo "$proto_file" | sed "s|^$PROTO_DIR/||")
    # 提取目录部分（不包含文件名）
    dir_part=$(dirname "$rel_path")
    # 计算输出文件的完整路径
    output_subdir="$OUTPUT_DIR/$dir_part"
    
    # 创建对应的输出子目录
    mkdir -p "$output_subdir"
    
    echo "Processing: $proto_file"
    # 运行protoc命令生成C#代码，使用--csharp_out参数指定输出目录
    # 这样会根据proto文件中的package声明和目录结构来组织生成的文件
    protoc --csharp_out="$OUTPUT_DIR" --proto_path="$PROTO_DIR" "$proto_file"
    
    # 如果需要显式地将生成的文件移动到对应的子目录
    # 首先获取文件名（不包含路径）
    filename=$(basename "$proto_file")
    # 移除.proto后缀，提取基本文件名
    base_filename="${filename%.proto}"
    # 转换为PascalCase并添加.cs后缀
    pascal_case_filename=$(to_pascal_case "$base_filename").cs
    
    # 如果生成的文件在根输出目录中，移动到对应的子目录并应用PascalCase命名
    cs_filename="${filename%.proto}.cs"
    if [ -f "$OUTPUT_DIR/$cs_filename" ]; then
        # 确保目标子目录存在
        mkdir -p "$output_subdir"
        # 移动文件并应用PascalCase命名
        mv "$OUTPUT_DIR/$cs_filename" "$output_subdir/$pascal_case_filename"
        echo "Moved and renamed $cs_filename to $dir_part/$pascal_case_filename"
    fi
done

echo "All C# code generated successfully with correct directory structure and PascalCase file naming!"