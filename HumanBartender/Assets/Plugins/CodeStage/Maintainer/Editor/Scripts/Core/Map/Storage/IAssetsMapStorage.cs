#region copyright
// -------------------------------------------------------
// Copyright (C) Dmitry Yuhanov [https://codestage.net]
// -------------------------------------------------------
#endregion

namespace CodeStage.Maintainer.Core.Map.Storage
{
    internal interface IAssetsMapStorage
    {
        AssetsMap Load(string path);
        void Save(string path, AssetsMap map);
        void Delete(string path);
    }
} 