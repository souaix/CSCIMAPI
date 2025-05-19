
// Recipe2DCodeService.cs
using Core.Interfaces;
using Core.Entities.LaserMarking;
using QRCoder;
using System.IO;
using Core.Entities.Public;
using Core.Entities.Recipe2DCodeGenerator;
using Infrastructure.Utilities;
using Org.BouncyCastle.Asn1.Ocsp;

namespace Infrastructure.Services
{
	public class Recipe2DCodeService : IRecipe2DCodeService
	{
		private readonly IRepositoryFactory _repositoryFactory;

		public Recipe2DCodeService(IRepositoryFactory repositoryFactory)
		{
			_repositoryFactory = repositoryFactory;
		}

		public async Task<ApiReturn<int>> Save2DCodeAsync(Recipe2DCodeRequest req)
		{
			string CombineField(string? value, int len) => (value ?? "").PadRight(len, ' ');

			string step = CombineField(req.Step, 10);
			string pn = CombineField(req.Pn, 30);
			string lotno = CombineField(req.Lotno, 30);
			string gbom = CombineField(req.Gbom, 46);
			string sequence = CombineField(req.Sequence, 4);
			string recipe = CombineField(req.Recipe, req.Length == 500 ? 380 : 180);
			string combined = step + pn + lotno + gbom + sequence + recipe;

			// 使用 DataMatrix 轉圖
			byte[] dmBytes = DataMatrixHelper.GenerateDataMatrix(combined);

			//var repo = _repositoryFactory.CreateRepository(req.Environment);
			//var (_, repo, _) = RepositoryHelper.CreateRepositories(req.Environment, _repositoryFactory);
			var repositories = RepositoryHelper.CreateRepositories(req.Environment, _repositoryFactory);
			// 使用某個特定的資料庫
			var repo = repositories["CsCimEmap"];

			string sql = @"
							MERGE INTO ARGOMESRECIPE2DCODE T
							USING (SELECT :Lotno AS LOTNO FROM DUAL) S
							ON (T.LOTNO = S.LOTNO)
							WHEN MATCHED THEN
								UPDATE SET
									T.LENGTH = :Length,
									T.STEP = :Step,
									T.PN = :Pn,
									T.GBOM = :Gbom,
									T.SEQUENCE = :Sequence,
									T.RECIPE = :Recipe,
									T.RECIPE2DCODE = :Recipe2DCode
							WHEN NOT MATCHED THEN
								INSERT (LOTNO, LENGTH, STEP, PN, GBOM, SEQUENCE, RECIPE, RECIPE2DCODE)
								VALUES (:Lotno, :Length, :Step, :Pn, :Gbom, :Sequence, :Recipe, :Recipe2DCode)
							";


			var param = new
			{
				req.Lotno,
				req.Length,
				req.Step,
				req.Pn,
				req.Gbom,
				req.Sequence,
				req.Recipe,
				Recipe2DCode = dmBytes
			};

			int rows = await repo.ExecuteAsync(sql, param);
			return ApiReturn<int>.Success("儲存成功", rows);
		}
	}
}