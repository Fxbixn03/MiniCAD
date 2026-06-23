using FluentAssertions;
using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Tests;

public class DocumentAuditTests
{
    [Fact]
    public void Audit_WithoutRepair_ReportsOrphanReference_ButDoesNotChangeIt()
    {
        var document = new CadDocument();
        var bogusLayer = Guid.NewGuid();
        var line = new LineEntity(Point2D.Origin, new Point2D(1, 1)) { LayerId = bogusLayer };
        document.AddEntity(line);

        DocumentAudit audit = DocumentAudit.Run(document, repair: false);

        audit.HasIssues.Should().BeTrue();
        audit.Findings.Should().Contain(f => !f.Repaired && f.Description.Contains("Layer"));
        line.LayerId.Should().Be(bogusLayer, "nothing is changed in check-only mode");
    }

    [Fact]
    public void Audit_WithRepair_RehomesOrphanReferenceToDefault()
    {
        var document = new CadDocument();
        var line = new LineEntity(Point2D.Origin, new Point2D(1, 1)) { LayerId = Guid.NewGuid() };
        document.AddEntity(line);

        DocumentAudit audit = DocumentAudit.Run(document, repair: true);

        audit.RepairedCount.Should().BeGreaterThan(0);
        line.LayerId.Should().Be(document.DefaultLayer.Id);
    }

    [Fact]
    public void Audit_WithRepair_RemovesOrphanBlockReferenceAndDegenerateGeometry()
    {
        var document = new CadDocument();
        document.AddEntity(new BlockReferenceEntity(Guid.NewGuid(), new Point2D(3, 3)));
        document.AddEntity(new PolylineEntity(new[] { Point2D.Origin }));
        document.AddEntity(new LineEntity(Point2D.Origin, new Point2D(1, 1)));

        DocumentAudit audit = DocumentAudit.Run(document, repair: true);

        document.Entities.Should().ContainSingle().Which.Should().BeOfType<LineEntity>();
        audit.Findings.Count(f => f.Severity == AuditSeverity.Error).Should().Be(2);
    }

    [Fact]
    public void Audit_OnHealthyDocument_FindsNothing()
    {
        var document = new CadDocument();
        document.AddEntity(new LineEntity(Point2D.Origin, new Point2D(1, 1)));

        DocumentAudit audit = DocumentAudit.Run(document, repair: false);

        audit.HasIssues.Should().BeFalse();
    }

    [Fact]
    public void LoadContents_SelfHeals_DamagedFile_WithoutThrowing()
    {
        var document = new CadDocument();
        var layer = new Layer("0", new StrokeStyle(Color.White, 1));
        var teilbild = new PartialDrawing("Teilbild 1");

        var good = new LineEntity(Point2D.Origin, new Point2D(1, 1)) { LayerId = layer.Id, PartialDrawingId = teilbild.Id };
        var orphanLayer = new LineEntity(Point2D.Origin, new Point2D(2, 2)) { LayerId = Guid.NewGuid(), PartialDrawingId = teilbild.Id };
        var orphanBlock = new BlockReferenceEntity(Guid.NewGuid(), new Point2D(3, 3));
        var degenerate = new PolylineEntity(new[] { Point2D.Origin });

        var contents = new DocumentContents(
            new[] { layer }, layer.Id, layer.Id,
            new[] { teilbild }, teilbild.Id,
            new IEntity[] { good, orphanLayer, orphanBlock, degenerate });

        Action load = () => document.LoadContents(contents);

        load.Should().NotThrow();
        document.Entities.Should().BeEquivalentTo(new IEntity[] { good, orphanLayer });
        orphanLayer.LayerId.Should().Be(document.DefaultLayer.Id, "the orphaned reference was healed to the default layer");
    }
}
