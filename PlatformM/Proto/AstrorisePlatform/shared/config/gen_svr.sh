#!/bin/bash

# 声明变量
lang=""

# 记录初始目录
originPath=$(pwd)
# 进入脚本目录
cd "$(dirname "$0")" || exit

source common.sh

WORKSPACE="$CONFIGX_WORKSPACE"
LUBAN_DLL=$WORKSPACE/Tools/Luban/Luban.dll
CONF_ROOT=.
OUTPUT_DATA_DIR="output_bin"
OUTPUT_CODE_DIR="output_svr"

generate_main_tables() {
	dotnet "$LUBAN_DLL" \
		-t server \
		-d bin \
		-c go-bin \
		--conf $CONF_ROOT/luban.conf \
		-x outputDataDir="$OUTPUT_DATA_DIR" \
		-x go-bin.outputCodeDir="$OUTPUT_CODE_DIR" \
		-x lubanGoModule=gitit.cc/social/astrorise/common/luban \
		--variant Lang.content="$lang"
}

SERVER_CONFIG="$AR_SERVER_WORKSPACE/arplatform/pconfig/lconfig"
SERVER_DATA="$SERVER_CONFIG/bytesdata"
SERVER_CODE="$SERVER_CONFIG/cfg"

cp_files_to_server() {
	mkdir -p "$SERVER_DATA"
	mkdir -p "$SERVER_CODE"
	# 删除原有文件
	find "$SERVER_DATA" -mindepth 1 -delete
#	find "$SERVER_CODE" -name "*.go" -type f
	find "$SERVER_CODE" -name "*.go" -type f -delete
	# 拷贝文件
	argenbytes -I "$OUTPUT_DATA_DIR" -O "$SERVER_DATA" -p bytesdata -e
	cp -r "$OUTPUT_CODE_DIR"/* "$SERVER_CODE/"
}

# 主执行流程
main() {
    check_prerequisites
    generate_main_tables
    cp_files_to_server

    log_info "=== 所有生成完成 ==="
}

# 执行主函数
main "$@"

# 回到初始目录
cd "$originPath" || exit
