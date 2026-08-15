#!/usr/bin/env python3

from argparse import ArgumentParser
from decimal import Decimal, InvalidOperation
from pathlib import Path
from xml.etree import ElementTree


def percentage(value: str, label: str) -> Decimal:
    try:
        rate = Decimal(value) * 100
    except InvalidOperation as error:
        raise ValueError(f"Invalid {label} rate in coverage report: {value}") from error

    if rate < 0 or rate > 100:
        raise ValueError(f"Invalid {label} rate in coverage report: {value}")

    return rate


def main() -> int:
    parser = ArgumentParser(description="Enforce Cobertura coverage for one assembly.")
    parser.add_argument("report", type=Path)
    parser.add_argument("assembly")
    parser.add_argument("minimum_line", type=Decimal)
    parser.add_argument("minimum_branch", type=Decimal)
    arguments = parser.parse_args()

    for name, threshold in (
        ("line", arguments.minimum_line),
        ("branch", arguments.minimum_branch),
    ):
        if threshold < 0 or threshold > 100:
            parser.error(f"minimum {name} coverage must be between 0 and 100")

    root = ElementTree.parse(arguments.report).getroot()
    packages = [
        package
        for package in root.findall("./packages/package")
        if package.get("name") == arguments.assembly
    ]

    if len(packages) != 1:
        names = ", ".join(
            sorted(
                package.get("name", "<unnamed>")
                for package in root.findall("./packages/package")
            )
        )
        raise ValueError(
            f"Expected one {arguments.assembly} package in {arguments.report}; "
            f"found {len(packages)}. Available packages: {names or '<none>'}"
        )

    package = packages[0]
    line = percentage(package.get("line-rate", ""), "line")
    branch = percentage(package.get("branch-rate", ""), "branch")

    print(
        f"{arguments.assembly}: line {line:.2f}% "
        f"(minimum {arguments.minimum_line:.2f}%), branch {branch:.2f}% "
        f"(minimum {arguments.minimum_branch:.2f}%)"
    )

    failures = []
    if line < arguments.minimum_line:
        failures.append(
            f"line coverage {line:.2f}% is below {arguments.minimum_line:.2f}%"
        )
    if branch < arguments.minimum_branch:
        failures.append(
            f"branch coverage {branch:.2f}% is below {arguments.minimum_branch:.2f}%"
        )

    if failures:
        print(f"Coverage gate failed for {arguments.assembly}: {'; '.join(failures)}")
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
