import os
import sys
from oletools.olevba import VBA_Parser

def extract_vba_modules(excel_path, output_dir="vba_output"):
    """
    从 Excel 文件中提取 VBA 模块并将它们保存为 .txt 文件。
    
    :param excel_path: Excel 文件的路径 (.xlsm, .xls, .xlsb)
    :param output_dir: 保存提取出的 txt 文件的文件夹
    """
    
    # 1. 检查文件是否存在
    if not os.path.exists(excel_path):
        print(f"错误: 找不到文件 '{excel_path}'")
        return

    # 2. 创建输出目录
    if not os.path.exists(output_dir):
        os.makedirs(output_dir)
        print(f"已创建输出目录: {output_dir}")

    print(f"正在分析: {excel_path} ...")

    try:
        # 3. 初始化 VBA 解析器
        vbaparser = VBA_Parser(excel_path)
        
        # 检查是否包含宏
        if not vbaparser.detect_vba_macros():
            print("未在文件中检测到 VBA 宏。")
            vbaparser.close()
            return

        count = 0
        # 4. 遍历并提取宏代码
        # extract_macros 返回生成器: (filename, stream_path, vba_filename, vba_code)
        for (filename, stream_path, vba_filename, vba_code) in vbaparser.extract_macros():
            """
            vba_filename通常是模块名称 (例如 Module1, ThisWorkbook)
            vba_code 是实际的代码字符串
            """
            
            # 处理文件名，防止包含非法字符
            safe_name = vba_filename.replace('/', '_').replace('\\', '_')
            output_filename = os.path.join(output_dir, f"{safe_name}.txt")

            # 将代码写入 txt 文件
            with open(output_filename, 'w', encoding='utf-8') as f:
                f.write(vba_code)
            
            print(f"已提取: {safe_name}.txt")
            count += 1

        print(f"\n完成！共提取了 {count} 个模块到 '{output_dir}' 文件夹。")
        vbaparser.close()

    except Exception as e:
        print(f"发生错误: {e}")

if __name__ == "__main__":
    #在此处修改你的 Excel 文件路径
    excel_file = "test.xlsm"  # 替换为你的实际文件路径
    
    # 你也可以通过命令行传参运行: python script.py file.xlsm
    if len(sys.argv) > 1:
        excel_file = sys.argv[1]

    extract_vba_modules(excel_file)