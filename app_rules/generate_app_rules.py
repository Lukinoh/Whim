from pathlib import Path

import yaml
from requests import get

# path to generated file
ROOT = Path(__file__).parents[0]
OUTFILE = ROOT / "DefaultFilteredWindowsKomorebi.g.cs"

# url of komorebi application rules
URL = "https://raw.githubusercontent.com/LGUG2Z/komorebi-application-specific-configuration/master/applications.yaml"

# portion of file above auto-generated rules
HEADER = """\
/* This file was generated from data with the following license:
 *
 * MIT License
 *
 * Copyright (c) 2021 Jade Iqbal
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

namespace Whim;

/// <summary>
/// This file is automatically generated by generate_app_rules.py. Do not edit it manually.
/// </summary>
internal static class DefaultFilteredWindowsKomorebi
{
	/// <summary>
	/// Load the windows ignored by Komorebi <see href="https://github.com/LGUG2Z/komorebi-application-specific-configuration"/>.
	/// </summary>
	/// <param name="filterManager"></param>
	public static void LoadWindowsIgnoredByKomorebi(IFilterManager filterManager)
	{\
"""

# portion of file below auto-generated rules
FOOTER = """\
	}
}
"""

# config options
TAB = "\t" * 2  # indention of auto-generated rules, " " * 8 or "\t" * 2
COMMENT = "// "  # comment string used for auto-generated content, "// " or "/// "


class GetRules:
	def __init__(self, url):
		self.url = url
		self.out = ROOT / "komorebi_rules.yaml"
		self.rules = None

	def download(self):
		response = get(self.url)
		with open(self.out, "wb") as f:
			f.write(response.content)

	def load_yaml(self):
		with open(self.out, "r") as f:
			self.rules = yaml.safe_load(f)


class GenerateRules:
	def __init__(self, komorebi_rules):
		self.komorebi_rules = komorebi_rules

	def generate_all_rules(self):
		for app in self.komorebi_rules:
			if "float_identifiers" in app:
				# windows with matching `float_identifiers` are ignored by komorebi
				Application(app["float_identifiers"], app["name"]).generate_rules()


class Application:
	def __init__(self, app_rules, app_name):
		self.app_name = app_name
		self.app_rules = app_rules

	def generate_rules(self):
		with open(OUTFILE, "a") as o:
			o.write("".join(["\n", TAB, COMMENT, self.app_name, "\n"]))
		for r in self.app_rules:
			CompositeRule(r).add_rule()


class CompositeRule:
	_processed = []

	def __init__(self, rule):
		self.rule = rule

	def add_rule(self):
		if isinstance(self.rule, list):
			rule = " && ".join([Rule(r, composite=True).make_rule() for r in self.rule])
		else:
			rule = Rule(self.rule, composite=False).make_rule()

		if rule[:13] != "filterManager":
			rule = f"filterManager.Add((window) => {rule});"

		if rule in self._processed:
			pre = TAB + "// "
			post = "  // duplicate rule"
		else:
			self._processed.append(rule)
			pre = TAB
			post = ""

		with open(OUTFILE, "a") as o:
			o.write("".join([pre, rule, post, "\n"]))


class Rule:
	def __init__(self, rule, composite=False):
		self.kind = rule["kind"]
		self.id = rule["id"]
		self.matching_strategy = rule[_] if (_ := "matching_strategy") in rule else "Legacy"
		self.composite = composite

		if self.kind not in ("Class", "Exe", "Title"):
			raise NotImplementedError("Unknown rule type: " + self.kind)

		# Look for negated matching strategies
		self.negated = self.is_negated()

		# "Legacy" maps to "Equals" for processes
		if self.matching_strategy == "Legacy" and self.kind == "Exe":
			self.matching_strategy = "Equals"

	def is_negated(self):
		if self.matching_strategy[:7] != "DoesNot":
			return False

		# Convert matching strategy to un-negated, plural form
		self.matching_strategy = self.matching_strategy[7:]
		match self.matching_strategy:
			case "Equal":
				self.matching_strategy = "Equals"
			case "Contain":
				self.matching_strategy = "Contains"
			case "EndWith":
				self.matching_strategy = "EndsWith"
			case "StartWith":
				self.matching_strategy = "StartsWith"

		return True

	def get_property_by_kind(self):
		return {"Class": "WindowClass", "Exe": "ProcessFileName", "Title": "Title"}[self.kind]

	def make_rule(self):
		if self.matching_strategy == "Equals" and not self.negated and not self.composite:
			scheme = 'filterManager.Add{0}Filter("{2}");'
		elif self.matching_strategy in {"Equals", "Contains", "EndsWith", "StartsWith"}:
			scheme = 'window.{}.{}("{}")'
		elif self.matching_strategy == "Legacy":
			scheme = '(window.{0}.StartsWith("{2}") || window.{0}.EndsWith("{2}"))'
		else:
			raise NotImplementedError("Unknown matching strategy: " + self.matching_strategy)

		rule = scheme.format(self.get_property_by_kind(), self.matching_strategy, self.id)
		return f"!{rule}" if self.negated else rule


# Add header
with open(OUTFILE, "w") as o:
	o.write(HEADER)

# Load Komorebi rules
komorebi_rules = GetRules(URL)
komorebi_rules.download()
komorebi_rules.load_yaml()

# Generate Whim rules
GenerateRules(komorebi_rules.rules).generate_all_rules()

# Add footer
with open(OUTFILE, "a") as o:
	o.write(FOOTER)

# vim: set ts=4 sw=4 noet :
