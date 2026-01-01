#!/bin/bash

set -e
# set -x

# 记录初始目录
originPath=$(pwd)
# 进入脚本目录
cd "$(dirname "$0")" || exit
# 进入上层项目目录，以此为工作区
cd "../.." || exit

protocalDir="../protocol"
protoDir="./shared/message/proto"
# 目标目录
pbDir="../"${1}"/client/Assets/Scripts/Shared/Proto/Messages/Common"
base_namespace=""

# 需要排除的目录模式
exclude_dirs=(
    "google/protobuf"
)

# 需要排除的文件模式
exclude_files=(
)

# 判定目录是否存在
if [ -d "$pbDir" ]; then
	# 清理之前生成文件
	echo "deleted files:"
	find "$pbDir" -maxdepth 10 -name "*.cs" -delete
else
	# 创建目录
	mkdir -p "$pbDir"
fi

# 检查路径是否匹配排除模式
should_exclude() {
    local file_path="$1"

    # 排除目录检查
    for exclude_dir in "${exclude_dirs[@]}"; do
        if [[ "$file_path" == "${exclude_dir%/}/"* ]]; then
            return 0
        fi
        if [[ "$file_path" == *"/${exclude_dir%/}/"* ]]; then
            return 0
        fi
    done
    
    # 排除文件检查
    local file_name=$(basename "$file_path")
    for exclude_file in "${exclude_files[@]}"; do
        if [[ "$file_name" == $exclude_file ]]; then
            return 0
        fi
    done

    return 1
}

create_dir() {
    local dir="$1"
    
    if [ -d "$dir" ]; then
        echo "目录已存在，清理现有内容: $dir"
        rm -rf "$dir"
        echo "已删除现有目录及其内容"
    fi
    
    echo "创建目录: $dir"
    mkdir -p "$dir"
    
    if [ $? -eq 0 ]; then
        echo "目录创建成功: $dir"
    else
        echo "目录创建失败: $dir"
        exit 1
    fi
}

# 递归查找proto文件（支持多目录）
find_proto_files() {
    local search_dirs=("$@")  # 接收多个目录参数
    local proto_files=()
    
    # 如果没有提供目录，使用当前目录
    if [ ${#search_dirs[@]} -eq 0 ]; then
        search_dirs=(".")
    fi
    
    echo "在以下目录中查找proto文件: ${search_dirs[*]}" >&2
    echo "排除目录模式: ${exclude_dirs[*]}" >&2
    echo "排除文件模式: ${exclude_files[*]}" >&2
    
    for search_dir in "${search_dirs[@]}"; do
        if [ ! -d "$search_dir" ]; then
            echo "警告: 目录 '$search_dir' 不存在，跳过" >&2
            continue
        fi
        
        echo "正在搜索目录: $search_dir" >&2

        local find_command=(find "$search_dir" -name "*.proto" -type f)
        for exclude_dir in "${exclude_dirs[@]}"; do
            find_command+=(-not -path "*/${exclude_dir%/}/*")
        done
        find_command+=(-print0)
        # echo "正在排除目录: $find_command" >&2

        # 使用find命令查找所有.proto文件
        while IFS= read -r -d '' file; do
            local filename=$(basename "$file")

            # 获取相对路径
            local relative_path="${file#$search_dir/}"
            # 如果search_dir是当前目录，去掉开头的./
            relative_path="${relative_path#./}"

            if should_exclude "$relative_path"; then
                echo "排除文件：$relative_path" >&2
                continue
            fi 

            proto_files+=("$file")
            # echo "包含proto文件: $relative_path" >&2

        done < <(find "$search_dir" -name "*.proto" -type f -print0 2>/dev/null)
    done
    
    # 只输出文件路径到stdout，每个文件一行
    printf '%s\n' "${proto_files[@]}"
}


# ============================================================================
# 主函数
# ============================================================================

main() {
      # 查找proto文件
    proto_files=()
    while IFS= read -r file; do
        proto_files+=("$file")
    done < <(find_proto_files "$protocalDir" "$protoDir")

    if [ ${#proto_files[@]} -eq 0 ]; then
        echo "没有找到符合条件的proto文件"
        exit 1
    fi

    echo "找到 ${#proto_files[@]} 个proto文件"

    # 创建输出目录
    create_dir "$pbDir"

    protoc --proto_path="$protocalDir" --proto_path="$protoDir" --csharp_out="$pbDir" --csharp_opt=base_namespace=$base_namespace "${proto_files[@]}"
}

# 运行主函数
main "$@" 


# 回到初始目录
cd "$originPath" || exit
