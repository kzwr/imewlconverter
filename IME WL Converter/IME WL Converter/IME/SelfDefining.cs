﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
using Studyzy.IMEWLConverter.Entities;
using Studyzy.IMEWLConverter.Filters;
using Studyzy.IMEWLConverter.Generaters;
using Studyzy.IMEWLConverter.Helpers;

namespace Studyzy.IMEWLConverter.IME
{
    [ComboBoxShow(ConstantString.SELF_DEFINING, ConstantString.SELF_DEFINING_C, 2000)]
    public class SelfDefining : BaseImport, IWordLibraryTextImport, IWordLibraryExport,IStreamPrepare
    {
        public override CodeType CodeType
        {
            get { return UserDefiningPattern.CodeType; }
        }
        public ParsePattern UserDefiningPattern { get; set; }

        private SelfDefiningCodeGenerater codeGenerater = new SelfDefiningCodeGenerater();
 

        #region IWordLibraryExport Members

        /// <summary>
        /// 导出词库为自定义格式。
        /// 如果没有指定自定义编码文件，而且词库是包含拼音编码的，那么就按拼音编码作为每个字的码。
        /// 如果导出指定了自定义编码文件，那么就忽略词库的已有编码，使用自定义编码文件重新生成编码。
        /// 如果词库没有包含拼音编码，而且导出也没有指定编码文件，那就抛错吧~~~~
        /// </summary>
        /// <param name="wlList"></param>
        /// <returns></returns>
        public string Export(WordLibraryList wlList)
        {
           Prepare();
            var sb = new StringBuilder();
            foreach (WordLibrary wordLibrary in wlList)
            {
                try
                {
                    sb.Append(ExportLine(wordLibrary));
                    sb.Append(UserDefiningPattern.LineSplitString);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }
            return sb.ToString();
        }

        private string lineFormat="";

        public void Prepare()
        {
            if (string.IsNullOrEmpty(UserDefiningPattern.MappingTablePath) &&
                UserDefiningPattern.CodeType == CodeType.UserDefine)
            {

                throw new Exception("未指定字符编码映射文件，无法对词库进行自定义编码的生成");

            }

            codeGenerater = new SelfDefiningCodeGenerater();
            if (UserDefiningPattern.CodeType == CodeType.Pinyin)
            {
                codeGenerater.MappingDictionary = PinyinHelper.PinYinDict;
            }
            else
            {
                var dict = UserCodingHelper.GetCodingDict(UserDefiningPattern.MappingTablePath,
                    UserDefiningPattern.TextEncoding);
                codeGenerater.MappingDictionary = dict;
            }
            codeGenerater.Is1Char1Code = UserDefiningPattern.IsPinyinFormat;
            codeGenerater.MutiWordCodeFormat = UserDefiningPattern.MutiWordCodeFormat;
            BuildLineFormat();
        }

        private void BuildLineFormat()
        {
            Dictionary<int, string> dictionary = new Dictionary<int, string>();
            if (UserDefiningPattern.ContainCode)
            {
                dictionary.Add(UserDefiningPattern.Sort[0], "{0}");
            }
            if (UserDefiningPattern.ContainRank)
            {
                dictionary.Add(UserDefiningPattern.Sort[2], "{2}");
            }
            dictionary.Add(UserDefiningPattern.Sort[1], "{1}");
            var newSort = new List<int>(UserDefiningPattern.Sort);
            newSort.Sort();
          
            lineFormat = "";
            foreach (int x in newSort)
            {
                if (dictionary.ContainsKey(x))
                {
                    lineFormat += dictionary[x] + UserDefiningPattern.SplitString;
                }
            }
            lineFormat = lineFormat.Substring(0, lineFormat.Length - UserDefiningPattern.SplitString.Length);
        }

        public string ExportLine(WordLibrary wl)
        {
            if (lineFormat == "")
            {
                BuildLineFormat();
            }
            List<string> lines=new List<string>();
            //需要判断源WL与导出的字符串的CodeType是否一致，如果一致，那么可以采用其编码，如果不一致，那么忽略编码，
            //调用CodeGenerater生成新的编码，并用新编码生成行
            IList<string> codes = null;
            if (wl.CodeType == this.CodeType)
            {
                codes = wl.GetCodeString(UserDefiningPattern.CodeSplitString, UserDefiningPattern.CodeSplitType);
               
            }
            else
            {
                codes = codeGenerater.GetCodeOfString(wl.Word, UserDefiningPattern.CodeSplitString,UserDefiningPattern.CodeSplitType);
                if (codes == null || codes.Count == 0) //生成失败
                    return null;
               
            }
            var word = wl.Word;
            var rank = wl.Rank;
            foreach (var code in codes)
            {
                var line = String.Format(lineFormat, code, word, rank);
                lines.Add(line);
            }

            return String.Join(UserDefiningPattern.LineSplitString,CollectionHelper.ToArray(lines));
        }

       
        #endregion

        #region IWordLibraryTextImport Members

        public Encoding Encoding
        {
            get { return UserDefiningPattern.TextEncoding; }
        }


        public WordLibraryList Import(string path)
        {
            string str = FileOperationHelper.ReadFile(path);
            return ImportText(str);
        }

        public WordLibraryList ImportText(string str)
        {
            var wlList = new WordLibraryList();
            string[] lines = str.Split(new[] {"\r", "\n"}, StringSplitOptions.RemoveEmptyEntries);
            CountWord = lines.Length;
            CountWord = lines.Length;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                CurrentStatus = i;
                wlList.AddWordLibraryList(ImportLine(line));
                CurrentStatus = i;
            }
            return wlList;
        }

        public WordLibraryList ImportLine(string line)
        {
            var wlList = new WordLibraryList();
            WordLibrary wl = BuildWordLibrary(line);
            wlList.Add(wl);
            return wlList;
        }
       

        #endregion

        #region 根据字符串生成WL

        /// <summary>
        /// 根据Pattern设置的格式，对输入的一行该格式的字符串转换成WordLibrary
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public WordLibrary BuildWordLibrary(string line)
        {
            var wl = new WordLibrary();
            wl.CodeType = UserDefiningPattern.CodeType;
            string[] strlist = line.Split(new[] { UserDefiningPattern.SplitString }, StringSplitOptions.RemoveEmptyEntries);
            var newSort = new List<int>(UserDefiningPattern.Sort);
            newSort.Sort();
            string code="", word="";
            int rank=0;
           
                int index1 =UserDefiningPattern. Sort.FindIndex(i => i == newSort[0]); //最小的一个
                if (index1 == 0 && UserDefiningPattern.ContainCode) //第一个是编码
                {
                    code = strlist[0];
                }
                if (index1 == 1)//第一个是汉字
                {
                    word= strlist[0];
                }
                if (index1 == 2 &&UserDefiningPattern. ContainRank)//第一个是词频
                {
                   rank = Convert.ToInt32(strlist[0]);
                }
                if (strlist.Length > 1)
                {
                    int index2 = UserDefiningPattern.Sort.FindIndex(i => i == newSort[1]); //中间的一个
                    if (index2 == 0 && UserDefiningPattern.ContainCode) //一个是Code
                    {
                        code = strlist[1];
                    }
                    if (index2 == 1)
                    {
                        word = strlist[1];
                    }
                    if (index2 == 2 && UserDefiningPattern.ContainRank)
                    {
                        rank = Convert.ToInt32(strlist[1]);
                    }
                }
                if (strlist.Length > 2)
                {
                    int index2 = UserDefiningPattern.Sort.FindIndex(i => i == newSort[2]); //最大的一个
                    if (index2 == 0 && UserDefiningPattern.ContainCode) //第一个是拼音
                    {
                        code = strlist[2];
                    }
                    if (index2 == 1)
                    {
                        word = strlist[2];
                    }
                    if (index2 == 2 && UserDefiningPattern.ContainRank)
                    {
                        rank = Convert.ToInt32(strlist[2]);
                    }
                }
            wl.Word = word;
            wl.Rank = rank;
            if (code != "")
            {
                if (UserDefiningPattern.IsPinyinFormat)
                {
                    var codes = code.Split(new[] {UserDefiningPattern.CodeSplitString},
                        StringSplitOptions.RemoveEmptyEntries);
                    wl.SetCode(UserDefiningPattern.CodeType,new List<string>(codes));
                }
                else
                {
                    wl.SetCode(UserDefiningPattern.CodeType,code);
                }
            }
          
            return wl;
        }
        #endregion

    }
}