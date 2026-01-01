#!/bin/bash

# 声明变量
LANGUAGES=()
lang=""

source common.sh

# 记录初始目录
originPath=$(pwd)
# 进入脚本目录
cd "$(dirname "$0")" || exit

WORKSPACE="$CONFIGX_WORKSPACE"
LUBAN_DLL=$WORKSPACE/Tools/Luban/Luban.dll
CONF_ROOT=.
OUTPUT_DATA_DIR="./output_bin"
OUTPUT_CODE_DIR="./output_cli"

# 第一次生成：排除 lang 表
generate_main_tables() {
    log_info "开始生成主表（排除 lang 表）..."

    dotnet "$LUBAN_DLL" \
        -t client \
        -d bin \
        -c cs-bin \
        --conf $CONF_ROOT/luban.conf \
		-x outputDataDir="$OUTPUT_DATA_DIR" \
		-x outputCodeDir="$OUTPUT_CODE_DIR" \
        --variant Lang.content="$lang"

	# 移除多余的多语言文件
	rm -f "$OUTPUT_DATA_DIR"/tblang.bytes
#	rm -f output_cli/TbLang.cs

    log_info "主表生成完成"
}

# 生成多语言文件
generate_language_files() {
    for lang in "${LANGUAGES[@]}"; do
        log_info "生成 $lang 语言文件..."

        # 临时输出目录
        TEMP_OUTPUT_DIR="./temp_$lang"
        mkdir -p "$TEMP_OUTPUT_DIR"
        TEMP_OUTPUT_CODE_DIR="./temp_code"
        mkdir -p "$TEMP_OUTPUT_CODE_DIR"

        # 生成指定语言
        dotnet "$LUBAN_DLL" \
            -t client \
            -d bin \
            -c cs-bin \
            --conf $CONF_ROOT/luban.conf \
            -x outputDataDir="$TEMP_OUTPUT_DIR" \
            -x outputCodeDir="$TEMP_OUTPUT_CODE_DIR" \
            --variant Lang.content="$lang"

        # 重命名文件
        if [ -f "$TEMP_OUTPUT_DIR/tblang.bytes" ]; then
            mv "$TEMP_OUTPUT_DIR/tblang.bytes" "$OUTPUT_DATA_DIR/tblang_${lang}.bytes"
            log_info "已生成: lang_${lang}.bytes"
        else
            log_error "未找到 tblang.bytes"
        fi
        
        # 清理临时目录
        rm -rf "$TEMP_OUTPUT_DIR"
        rm -rf "$TEMP_OUTPUT_CODE_DIR"
    done
}

CLIENT_WORKSPACE="../../../client/${1}"
CLIENT_DATA="$CLIENT_WORKSPACE/Assets/AssetsPackage/Configs/Common/DataTables"
CLIENT_CODE="$CLIENT_WORKSPACE/Assets/Scripts/Shared/Configs/Common/DataTables"

cp_files_to_client() {
	cp -r "$OUTPUT_DATA_DIR"/* "$CLIENT_DATA/"
	cp -r "$OUTPUT_CODE_DIR"/* "$CLIENT_CODE/"
}

# 主执行流程
main() {
    check_prerequisites

    # 判定目录是否存在
    if [ -d "$CLIENT_DATA" ]; then
        # 清理之前生成文件
        echo "deleted config files:"
        find "$CLIENT_DATA" -maxdepth 10 -name "*.bytes" -delete
    else
        # 创建目录
        mkdir -p "$CLIENT_DATA"
    fi

    # 判定目录是否存在
    if [ -d "$CLIENT_CODE" ]; then
        # 清理之前生成文件
        echo "deleted code files:"
        find "$CLIENT_CODE" -maxdepth 10 -name "*.cs" -delete
    else
        # 创建目录
        mkdir -p "$CLIENT_CODE"
    fi
    
    generate_main_tables
    generate_language_files
    cp_files_to_client

    log_info "=== 所有生成完成 ==="
}

# 执行主函数
main "$@"

# 回到初始目录
cd "$originPath" || exit